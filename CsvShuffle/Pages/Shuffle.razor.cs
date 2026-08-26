using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;

namespace CsvShuffle.Pages;

public partial class Shuffle : ComponentBase
{
    [Inject]
    ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    IDialogService DialogService { get; set; } = null!;

    string _fileName = string.Empty;
    string _search = string.Empty;
    string _progressLabel = "Preparing export…";
    string? _originalCsv;
    string? _obfuscatedCsv;
    bool _busy;
    double _progress;
    CancellationTokenSource? _cancellation;
    List<string> _headers = [];
    List<string[]> _rows = [];
    List<CsvRow> _gridRows = [];
    List<CsvRow> _obfuscatedGridRows = [];
    List<ObfuscationMode> _modes = [];
    bool _showObfuscated;

    static string AppVersion => typeof(Shuffle).Assembly
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                    .InformationalVersion
                                ?? "unknown";

    IEnumerable<CsvRow> VisibleGridRows => _showObfuscated
        ? _obfuscatedGridRows
        : _gridRows;

    string HeaderStatus => _headers.Count == 0
        ? "Up to 500 MB · UTF-8 recommended"
        : _progressLabel;

    void SetObfuscationMode(int columnIndex, ObfuscationMode mode) => _modes[columnIndex] = mode;

    async Task LoadFile(InputFileChangeEventArgs args)
    {
        await CancelActiveOperationAsync();
        ClearFile();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _fileName = args.File.Name;
        _busy = true;
        _progress = 0;
        _progressLabel = "Loading CSV… 0%";
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            await using var stream = args.File.OpenReadStream(500_000_000L);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string input = await ReadFileAsync(reader, args.File.Size, cancellation.Token);
            List<string[]> parsed = ParseCsv(input);

            if (parsed.Count == 0)
                throw new InvalidDataException("The selected file is empty.");

            _originalCsv = input;
            _headers = [.. parsed[0]];
            _rows = [.. parsed.Skip(1).Select(row => NormalizeRow(row, _headers.Count))];
            _gridRows = [.. _rows.Select(row => new CsvRow(row))];
            _obfuscatedGridRows.Clear();
            _modes = [.. Enumerable.Repeat(ObfuscationMode.Clear, _headers.Count)];
            _obfuscatedCsv = null;
            _showObfuscated = false;
            _progress = 100;
            _progressLabel = "CSV loaded.";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_cancellation, cancellation))
                _progressLabel = "Loading cancelled.";
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(_cancellation, cancellation))
                return;

            _headers.Clear();
            _rows.Clear();
            _gridRows.Clear();
            _obfuscatedGridRows.Clear();
            _originalCsv = null;
            _showObfuscated = false;
            string message = $"Could not read this CSV: {exception.Message}";
            Snackbar.Add(message, Severity.Error, options => options.RequireInteraction = true);
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
                _busy = false;
            }

            cancellation.Dispose();
        }
    }

    async Task Obfuscate()
    {
        await CancelActiveOperationAsync();

        _busy = true;
        _progress = 0;
        _progressLabel = "Preparing obfuscation…";
        _obfuscatedCsv = null;
        _obfuscatedGridRows.Clear();
        _showObfuscated = false;
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;

        try
        {
            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            if (_modes.All(mode => mode == ObfuscationMode.Clear))
            {
                _obfuscatedCsv = _originalCsv;
                _obfuscatedGridRows = [.. _gridRows];
                _progress = 100;
                _progressLabel = "No columns selected. Your file is ready.";
                Snackbar.Add("No columns selected. Your original CSV is ready to save.", Severity.Info);
                return;
            }

            Dictionary<string, string> consistentValues = [];
            IReadOnlyList<string>[] genericOptionsByColumn =
            [
                .. Enumerable.Range(0, _headers.Count)
                    .Select(column => _modes[column] == ObfuscationMode.GenericOption
                        ? _rows.Select(row => row[column])
                            .Where(value => !string.IsNullOrEmpty(value))
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                        : [])
            ];
            IReadOnlyList<string>[] eopDomainOptionsByColumn =
            [
                .. Enumerable.Range(0, _headers.Count)
                    .Select(column => _modes[column] == ObfuscationMode.EopEmail
                        ? _rows.Select(row => ObfuscationRules.GetEopDomainOption(row[column]))
                            .OfType<string>()
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                        : [])
            ];
            StringBuilder output = new();
            List<CsvRow> obfuscatedRows = [];
            output.AppendLine(string.Join(',', _headers.Select(EncodeCsv)));

            for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                Dictionary<string, string> rowTokens = [];

                string[] values =
                [
                    .. _rows[rowIndex].Select((value, column) => _modes[column] switch
                    {
                        ObfuscationMode.GenericOption => ObfuscationRules.TransformGenericOption(
                            value,
                            genericOptionsByColumn[column]
                        ),
                        ObfuscationMode.EopEmail => ObfuscationRules.TransformEop(
                            value,
                            eopDomainOptionsByColumn[column],
                            rowTokens
                        ),
                        _ => ObfuscationRules.Transform(
                            value,
                            _modes[column],
                            consistentValues,
                            rowTokens
                        )
                    })
                ];

                obfuscatedRows.Add(new CsvRow(values));
                output.AppendLine(string.Join(',', values.Select(EncodeCsv)));

                if (rowIndex % 500 != 0)
                    continue;

                _progress = 100d * rowIndex / Math.Max(1, _rows.Count);
                _progressLabel = $"Obfuscating row {rowIndex:N0} of {_rows.Count:N0}";
                await InvokeAsync(StateHasChanged);
                await Task.Yield();
            }

            _obfuscatedCsv = output.ToString();
            _obfuscatedGridRows = obfuscatedRows;
            _showObfuscated = true;
            _progress = 100;
            _progressLabel = "Obfuscation complete. Your file is ready.";
            Snackbar.Add("Obfuscation complete. Your file is ready.", Severity.Success);
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_cancellation, cancellation))
                _progressLabel = "Obfuscation cancelled.";
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(_cancellation, cancellation))
                return;

            _obfuscatedCsv = null;
            _obfuscatedGridRows.Clear();
            _showObfuscated = false;
            _progressLabel = "Obfuscation could not complete.";
            Snackbar.Add($"Obfuscation failed: {exception.Message}", Severity.Error, options => options.RequireInteraction = true);
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
                _busy = false;
            }

            cancellation.Dispose();
        }
    }

    async Task Download()
    {
        if (_obfuscatedCsv is not null)
            await Js.InvokeVoidAsync("csvShuffle.download", ObfuscatedFileName(), _obfuscatedCsv);
    }

    async Task ConfirmClearFile()
    {
        var parameters = new DialogParameters<ClearFileDialog>
        {
            { dialog => dialog.FileName, _fileName }
        };
        var dialog = await DialogService.ShowAsync<ClearFileDialog>("Clear CSV", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            ClearFile();
    }

    void ClearFile()
    {
        _fileName = string.Empty;
        _search = string.Empty;
        _progress = 0;
        _progressLabel = "Preparing export…";
        _headers.Clear();
        _rows.Clear();
        _gridRows.Clear();
        _obfuscatedGridRows.Clear();
        _modes.Clear();
        _originalCsv = null;
        _obfuscatedCsv = null;
        _showObfuscated = false;
    }

    async Task CancelActiveOperationAsync()
    {
        if (_cancellation is not null)
            await _cancellation.CancelAsync();
    }

    void Cancel() => _cancellation?.Cancel();

    string ObfuscatedFileName() => $"{Path.GetFileNameWithoutExtension(_fileName)}_obfuscated.csv";

    static string[] NormalizeRow(string[] row, int columnCount) =>
        [.. row.Concat(Enumerable.Repeat(string.Empty, Math.Max(0, columnCount - row.Length))).Take(columnCount)];

    static string EncodeCsv(string value) =>
        value.Contains(',') ||
        value.Contains('"') ||
        value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    async Task<string> ReadFileAsync(
        StreamReader reader,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        var input = new StringBuilder();
        char[] buffer = new char[64 * 1024];
        int reads = 0;

        while (true)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
                break;

            input.Append(buffer, 0, count);

            if (++reads % 4 != 0)
                continue;

            _progress = Math.Min(99, 100d * reader.BaseStream.Position / Math.Max(1, fileSize));
            _progressLabel = $"Loading CSV… {_progress:N0}%";
            await InvokeAsync(StateHasChanged);
            await Task.Yield();
        }

        return input.ToString();
    }

    static List<string[]> ParseCsv(string input)
    {
        List<string[]> rows = [];
        List<string> row = [];
        var cell = new StringBuilder();
        bool quoted = false;

        char delimiter = input.Count(character => character == '\t') > input.Count(character => character == ',')
            ? '\t'
            : ',';

        for (int i = 0; i < input.Length; i++)
        {
            char character = input[i];
            if (character == '"' && (quoted || cell.Length == 0))
            {
                if (quoted && i + 1 < input.Length && input[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (character == delimiter && !quoted)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (character is '\r' or '\n' && !quoted)
            {
                if (character == '\r' && i + 1 < input.Length && input[i + 1] == '\n')
                    i++;

                row.Add(cell.ToString());
                cell.Clear();
                if (row.Any(value => value.Length > 0))
                    rows.Add([.. row]);
                row = [];
            }
            else cell.Append(character);
        }

        if (cell.Length <= 0 && row.Count <= 0)
            return rows;

        row.Add(cell.ToString());
        rows.Add([.. row]);

        return rows;
    }
}
