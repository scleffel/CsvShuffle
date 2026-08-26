# CsvShuffle contributor guide

## Application overview

CsvShuffle is a client-side MudBlazor WebAssembly PWA for locally loading CSV files,
selecting sensitive columns, producing shape-preserving obfuscated data, previewing the
result, and downloading `<original-name>_obfuscated.csv`. It is an internal-tool UI:
keep changes focused, fast, accessible, and free of decorative or marketing-style UI.

## Current release

- The current release is **1.7.0**.
- `CsvShuffle/Version.props` is the single source of release version information.
  Maintain `Version`, `AssemblyVersion`, and `FileVersion` together when preparing a
  later release. The header displays `InformationalVersion`.
- The version currently in the working tree is authoritative; do not hard-code a
  separate display version in a component, manifest, chart, or workflow.

## Repository layout

- `CsvShuffle/` — .NET 10 Blazor WebAssembly PWA application.
- `CsvShuffle/Pages/Shuffle.razor` and `.razor.cs` — the primary upload, preview,
  transformation, progress, cancellation, and download workflow.
- `CsvShuffle/Pages/ShuffleTopBar.razor` — application identity, file picker,
  local-only indicator, theme control, and available-update action.
- `CsvShuffle/Pages/ShuffleToolbar.razor` and `ShuffleDataGrid.razor` — the loaded
  file controls and virtualized CSV table.
- `CsvShuffle/ObfuscationMode.cs` and `ObfuscationRules.cs` — supported transformations
  and their shape-preserving rules.
- `CsvShuffle/Layout/` — application shell, PWA-update detection, and theme-preference
  JavaScript interop.
- `CsvShuffle/wwwroot/` — static PWA, JavaScript, CSS, manifest, and icon assets.
- `.github/workflows/publish.yaml` and `helm/` — release and deployment packaging.

## Functional invariants

- Process files locally in the browser; do not add server upload, telemetry, or logging
  of CSV contents.
- Do not add third-party analytics or external calls that can expose file metadata or
  CSV contents. The “Local only” label in the top bar must remain truthful.
- Preserve CSV/data shape. Headers remain unchanged and non-alphanumeric characters,
  whitespace, accents, punctuation, and field structure must be retained whenever a
  transformation rule requires it. Accept valid quoted CSV and do not assume clean data.
- Default every column to `Clear`. Re-running **Obfuscate** must transform the original
  input, not a previous result.
- Keep identical source sensitive values consistently obfuscated within an output. In
  particular, duplicate SSNs must yield the same replacement.
- Keep large-file behavior in mind (100k+ rows): retain progress updates and cancellation
  points, and avoid needless full-data copies or rendering all rows at once.
- Maintain the data-grid experience: universal and column filters, sorting, resizable
  columns, sticky header, virtualization, and page sizes of 100, 500, and 1000.
- Keep the interface concise and operational: it is an internal tool, not a marketing
  surface. Prefer MudBlazor components and accessible labels/tooltips; avoid decorative
  copy and graphics.

## Obfuscation rules

- **Name:** replace letters while retaining case and vowel/consonant class; preserve
  non-letters.
- **Middle Name:** apply the Name rule, except preserve `NMN` (case-insensitive, with
  surrounding whitespace retained) unchanged.
- **Skip For:** apply the Middle Name rule, except preserve a cell unchanged when it
  contains a configured exclusion (case-insensitive). Global exclusions apply to every
  Skip For column, while a selected Skip For column can also have its own exclusions.
- **Date:** parse valid date/date-time values and vary day by ±10 days, month by ±2
  months, and year by ±5 years while producing a valid date. For an unparseable value,
  use the generic text behavior.
- **SSN:** replace digits only, preserving every non-digit character and the input
  shape. Identical SSNs must use the same replacement within one output.
- **Phone:** replace digits while preserving the input shape; retain a leading country
  digit and valid NANP leading digits where the specialized rule applies. Identical phone
  values must use the same replacement within one output.
- **Address:** retain address-unit terms; convert cardinal directions and recognized road
  types (including full and abbreviated forms) to another direction or road type while
  preserving casing; otherwise apply vowel/consonant letter replacement and digit
  replacement while preserving other characters.
- **Email:** obfuscate the local part and domain labels, retain the `@`/dot structure,
  and replace recognized top-level domains with another supported TLD. Identical values
  must use the same replacement within one output.
- **UPN:** for a value in `{name-or-ID}@{domain}.{tld}` form, apply the Generic rule to
  the portion before `@` and retain the domain and TLD exactly. For an invalid value, use
  the Generic rule for the complete value.
- **EOP:** for a value in `{name}@{dom0}.{dom1}.{tld}` form, apply the Generic rule to
  the name, replace `dom0` with a distinct `dom0` selected from that column, and retain
  `{dom1}.{tld}` exactly. For an invalid value, use the Generic rule for the complete value.
- **Relationship:** replace recognized relationship terms while retaining surrounding
  text and casing. Identical values must use the same replacement within one output.
- **Bracket Preserving:** use the vowel/consonant text rule outside balanced `()`, `[]`,
  and `{}` content; retain bracketed content unchanged.
- **Generic:** replace letters with letters and digits with digits; preserve all other
  characters.
- **Generic Option:** replace each non-empty cell independently with a randomly selected
  distinct non-empty value from the same column when one is available. Comparison is
  case-sensitive, and blank cells remain blank.

## Workflow expectations

- A selected CSV may be up to 500 MB and UTF-8 is recommended. Keep loading and
  obfuscation progress visible, and preserve cancellation behavior.
- Save/download output as `<original-name>_obfuscated.csv`. A no-op selection still
  produces an export of the original input.
- Do not turn the application into a desktop executable or add a server-side processing
  path without an explicit product decision; this is a client-side WebAssembly PWA.
- When changing PWA behavior, verify both a cold load and the update-available flow.
  When changing the top bar, retain keyboard-accessible CSV selection, color-mode
  cycling, the local-only affordance, and the version/update presentation.

## Development conventions

- Use MudBlazor components for UI changes and keep the existing concise visual style.
- Keep C# nullable-safe and use the project’s existing code style (file-scoped namespaces,
  explicit methods, and collection expressions where appropriate).
- Check the working tree before editing and preserve unrelated user changes.
- Build with `dotnet build CsvShuffle/CsvShuffle.csproj`; run relevant tests if they are
  added or available. Validate both the original and obfuscated table views after UI
  changes.
- Update documentation when user-visible behavior, supported transformations, privacy
  guarantees, or release/deployment behavior changes.

## Release notes

Publishing is driven by merging a version change to `main`. The workflow creates the
corresponding GitHub release and image/chart packages. See `README.md` for the exact
release behavior.
