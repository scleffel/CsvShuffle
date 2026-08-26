namespace CsvShuffle;

public sealed record SkipForTermGroup(
    int? ColumnIndex,
    string Name,
    IReadOnlyCollection<string> Terms
);