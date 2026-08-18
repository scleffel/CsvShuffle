# CsvShuffle contributor guide

## Application overview

CsvShuffle is a client-side MudBlazor WebAssembly PWA for locally loading CSV files,
selecting sensitive columns, producing shape-preserving obfuscated data, previewing the
result, and downloading `<original-name>_obfuscated.csv`. It is an internal-tool UI:
keep changes focused, fast, accessible, and free of decorative or marketing-style UI.

## Current release

- The most recent update is **1.1.0.0**.
- `CsvShuffle/Version.props` is the single source of release version information.
  Maintain `Version`, `AssemblyVersion`, and `FileVersion` together when preparing a
  later release. The header displays the assembly informational version.

## Repository layout

- `CsvShuffle/` — .NET 10 Blazor WebAssembly PWA application.
- `CsvShuffle/Pages/Shuffle.razor` and `.razor.cs` — the primary upload, preview,
  transformation, progress, cancellation, and download workflow.
- `CsvShuffle/ObfuscationMode.cs` — supported column transformations.
- `CsvShuffle/Layout/` — application shell and theme-preference JavaScript interop.
- `CsvShuffle/wwwroot/` — static PWA, JavaScript, CSS, manifest, and icon assets.
- `.github/workflows/publish.yaml` and `helm/` — release and deployment packaging.

## Functional invariants

- Process files locally in the browser; do not add server upload, telemetry, or logging
  of CSV contents.
- Preserve CSV/data shape. Headers remain unchanged and non-alphanumeric characters,
  whitespace, accents, punctuation, and field structure must be retained whenever the
  transformation rule requires it.
- Default every column to `Clear`. Re-running **Obfuscate** must transform the original
  input, not a previous result.
- Keep identical source sensitive values consistently obfuscated within an output. In
  particular, duplicate SSNs must yield the same replacement.
- Support dirty CSV values and valid CSV quoting. Do not assume all records are clean.
- Keep large-file behavior in mind (100k+ rows): retain progress updates and cancellation
  points, and avoid needless full-data copies or rendering all rows at once.
- Maintain the data-grid experience: universal and column filters, sorting, resizable
  columns, sticky header, virtualization, and page sizes of 100, 500, and 1000.

## Obfuscation rules

- **Name:** replace ASCII letters only; vowel-to-vowel and consonant-to-consonant,
  preserving case and all other characters.
- **Date:** parse valid date/date-time values and vary day by ±10 days, month by ±2
  months, and year by ±5 years while producing a valid date.
- **SSN / Phone:** replace digits only, preserving every non-digit character and the
  input shape.
- **Address:** apply letter rules and replace digits, preserving all other characters.
- **Generic:** replace letters with letters and digits with digits; preserve all other
  characters.

## Development conventions

- Use MudBlazor components for UI changes and keep the existing concise visual style.
- Keep C# nullable-safe and use the project’s existing code style (file-scoped namespaces,
  explicit methods, and collection expressions where appropriate).
- Check the working tree before editing and preserve unrelated user changes.
- Build with `dotnet build CsvShuffle/CsvShuffle.csproj`; run relevant tests if they are
  added or available. Validate both the original and obfuscated table views after UI
  changes.

## Release notes

Publishing is driven by merging a version change to `main`. The workflow creates the
corresponding GitHub release and image/chart packages. See `README.md` for the exact
release behavior.
