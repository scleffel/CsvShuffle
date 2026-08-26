# CsvShuffle

CsvShuffle is a client-side MudBlazor WebAssembly PWA for creating safe, shape-preserving
CSV copies. Select a CSV, assign an obfuscation mode to any columns, preview the result,
and download `<original-name>_obfuscated.csv`.

CSV contents are processed in the browser only. The application does not upload the CSV
to a server, collect telemetry, or log its contents.

## Using the app

1. Select a CSV file (up to 500 MB; UTF-8 is recommended).
2. For each sensitive column, choose an obfuscation mode from its column header. Every
   column begins as **Clear**.
   For **Skip For**, use the toolbar's exclusions control to manage terms shared by all
   Skip For columns or terms specific to an individual selected column.
3. Use the global search, column filters, sorting, resizing, and paging controls to
   inspect the data. The table supports 100, 500, and 1000 rows per page.
4. Choose **Obfuscate** to generate the obfuscated preview. You can switch between the
   original and obfuscated views.
5. Choose **Save** to download the result. Selecting no modes exports the original file.

Loading and obfuscation expose progress and can be cancelled. Running **Obfuscate** again
always starts with the original input, not a previous result.

## Obfuscation modes

| Mode | Behavior |
| --- | --- |
| Clear | Leaves the value unchanged. |
| Name | Replaces letters while retaining case, vowel/consonant class, and non-letters. |
| Middle Name | Applies the Name behavior while leaving `NMN` (no middle name) unchanged. |
| Skip For | Applies the Middle Name behavior, except cells containing configured exclusions remain unchanged. Global exclusions apply to every Skip For column; each selected column can also have its own exclusions. |
| SSN | Replaces digits while retaining all non-digit characters; duplicate values remain consistent in an output. |
| Date | Changes valid dates by up to ±10 days, ±2 months, and ±5 years while keeping a valid date. |
| Phone | Replaces digits while retaining formatting; duplicate values remain consistent in an output. |
| Address | Obfuscates address letters and digits while retaining formatting, address-unit terms, and cardinal-direction semantics. |
| Email | Obfuscates the local part and domain while retaining email structure. |
| UPN | Obfuscates only the name or ID before `@` using Generic rules; retains the domain and TLD. |
| EOP | Obfuscates the name using Generic rules, replaces the first domain label with another value from the column, and retains the final two domain labels. |
| Relationship | Replaces recognized relationship terms while retaining surrounding text. |
| Bracket Preserving | Obfuscates outside balanced parentheses, brackets, and braces while leaving their contents unchanged. |
| Generic | Replaces letters and digits and retains all other characters. |
| Generic Option | Replaces each non-empty cell independently with another non-empty value from the same column when available; blank cells remain blank. |

The app supports quoted CSV fields and dirty values. It keeps headers unchanged and
preserves field shape wherever the selected mode requires it.

## Development

This repository contains a .NET 10 Blazor WebAssembly PWA using MudBlazor.

```sh
dotnet build CsvShuffle/CsvShuffle.csproj
```

For implementation constraints and verification expectations, see [AGENTS.md](AGENTS.md).

## Releases and versioning

`CsvShuffle/Version.props` is the single release-version source. Change its `Version`
value (for example, from `1.0.0` to `1.1.0`) in a pull request. When that pull
request is merged into `main`, the publish workflow automatically:

- creates a GitHub release and `<version>` tag;
- publishes `ghcr.io/<owner>/csvshuffle:<version>`;
- packages and publishes the Helm chart at the same version, with the same
  `appVersion` and default Kubernetes image tag.

The application header also reads that build version, rather than maintaining a
separate display value. A deployment environment can set `image.tag` to that same
version (for example, `1.2.3`) in its `values.yaml`; no `v` prefix is used for Git,
image, or Helm-chart versions.

If the version has not changed, the workflow completes without republishing it.
