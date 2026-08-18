using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;

namespace CsvShuffle.Pages;

public partial class Shuffle : ComponentBase
{
    [Inject] ISnackbar Snackbar { get; set; } = null!;
    [Inject] IDialogService DialogService { get; set; } = null!;

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

    static string AppVersion => typeof(Shuffle)
                                    .Assembly
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                    .InformationalVersion
                                ?? "unknown";

    IEnumerable<CsvRow> VisibleGridRows => _showObfuscated
        ? _obfuscatedGridRows
        : _gridRows;

    string HeaderStatus => _headers.Count == 0
        ? "Up to 500 MB · UTF-8 recommended"
        : _progressLabel;

    bool QuickFilter(CsvRow row) =>
        string.IsNullOrWhiteSpace(_search)
        || row.Cells.Any(cell => cell.Contains(_search, StringComparison.OrdinalIgnoreCase));

    string? GetCellClass(int columnIndex) => _modes[columnIndex] == ObfuscationMode.Clear
        ? null
        : "obfuscated-cell";

    static string ObfuscationModeLabel(ObfuscationMode mode) => mode == ObfuscationMode.BracketPreserving
        ? "Bracket Preserving"
        : mode.ToString();

    void SetObfuscationMode(int columnIndex, ObfuscationMode mode) => _modes[columnIndex] = mode;

    async Task LoadFile(InputFileChangeEventArgs args)
    {
        if (_cancellation is not null)
            await _cancellation.CancelAsync();

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
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
            string input = await ReadFileAsync(reader, args.File.Size, _cancellation.Token);
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
            _progressLabel = "Loading cancelled.";
        }
        catch (Exception exception)
        {
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
            _busy = false;
        }
    }

    async Task Obfuscate()
    {
        _busy = true;
        _progress = 0;
        _progressLabel = "Preparing obfuscation…";
        _obfuscatedCsv = null;
        _obfuscatedGridRows.Clear();
        _showObfuscated = false;
        _cancellation = new CancellationTokenSource();

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
            StringBuilder output = new();
            List<CsvRow> obfuscatedRows = [];
            output.AppendLine(string.Join(',', _headers.Select(EncodeCsv)));

            for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                Dictionary<string, string> rowTokens = [];

                string[] values =
                [
                    .. _rows[rowIndex].Select((value, column) => Transform(
                        value,
                        _modes[column],
                        consistentValues,
                        rowTokens
                    ))
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
            _progressLabel = "Obfuscation cancelled.";
        }
        finally
        {
            _busy = false;
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

    static string Transform(
        string value,
        ObfuscationMode mode,
        Dictionary<string, string> consistentValues,
        Dictionary<string, string> rowTokens
    )
    {
        if (mode == ObfuscationMode.Clear || string.IsNullOrEmpty(value))
            return value;

        if (mode is not (ObfuscationMode.Ssn or ObfuscationMode.Phone))
        {
            return mode == ObfuscationMode.Date
                ? TransformDate(
                    value: value
                )
                : TransformText(
                    value: value,
                    mode: mode,
                    rowTokens: rowTokens
                );
        }

        string key = $"{mode}|{value}";

        if (consistentValues.TryGetValue(key, out string? prior))
            return prior;

        string transformed = TransformDigits(value);
        consistentValues[key] = transformed;
        return transformed;
    }

    static string TransformText(string value, ObfuscationMode mode, Dictionary<string, string> rowTokens)
    {
        var result = new StringBuilder(value.Length);
        bool preserveVowelClass =
            mode is ObfuscationMode.Name or ObfuscationMode.Address or ObfuscationMode.BracketPreserving;
        int bracketDepth = 0;
        bool hasAddressDigits = false;

        for (int index = 0; index < value.Length;)
        {
            char character = value[index];
            if (mode == ObfuscationMode.BracketPreserving)
            {
                switch (character)
                {
                    case '(' or '[' or '{':
                        bracketDepth++;
                        break;
                    case ')' or ']' or '}' when bracketDepth > 0:
                        bracketDepth--;
                        break;
                }

                if (bracketDepth > 0 || character is ')' or ']' or '}')
                {
                    result.Append(character);
                    index++;
                    continue;
                }
            }

            if (char.IsLetter(character))
            {
                int end = index + 1;
                while (end < value.Length && char.IsLetter(value[end]))
                    end++;

                string token = value[index..end];
                string key = $"{mode}|{token}";
                if (!rowTokens.TryGetValue(key, out string? replacement))
                {
                    replacement =
                        new string(token.Select(letter => RandomLetter(letter, preserveVowelClass)).ToArray());
                    rowTokens[key] = replacement;
                }

                result.Append(replacement);
                index = end;
                continue;
            }

            if (char.IsDigit(character))
            {
                bool firstAddressDigit = mode == ObfuscationMode.Address && !hasAddressDigits;
                result.Append(firstAddressDigit && character != '0'
                    ? (char)('1' + Random.Shared.Next(9))
                    : (char)('0' + Random.Shared.Next(10)));
                hasAddressDigits = hasAddressDigits || mode == ObfuscationMode.Address;
                index++;
                continue;
            }

            result.Append(character);
            index++;
        }

        return result.ToString();
    }

    static string TransformDigits(string value) =>
        new(value.Select(character => char.IsDigit(character)
            ? (char)('0' + Random.Shared.Next(10))
            : character).ToArray());

    static char RandomLetter(
        char source,
        bool preserveVowelClass
    )
    {
        const string vowels = "aeiouy";
        const string consonants = "bcdfghjklmnpqrstvwxz";

        string pool = preserveVowelClass && vowels.Contains(char.ToLowerInvariant(source))
            ? vowels
            : preserveVowelClass
                ? consonants
                : "abcdefghijklmnopqrstuvwxyz";

        char result = pool[Random.Shared.Next(pool.Length)];

        return char.IsUpper(source)
            ? char.ToUpperInvariant(result)
            : result;
    }

    static string TransformDate(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var date)
            && !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            return TransformText(
                value: value,
                mode: ObfuscationMode.Generic,
                rowTokens: []
            );

        return date
            .AddYears(Random.Shared.Next(-5, 6))
            .AddMonths(Random.Shared.Next(-2, 3))
            .AddDays(Random.Shared.Next(-10, 11))
            .ToString("M/d/yyyy", CultureInfo.InvariantCulture);
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
