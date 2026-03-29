namespace TcBatchRename;

internal sealed class RenamePlan
{
    public RenamePlan(IReadOnlyList<RenamePlanItem> items)
    {
        Items = items;
        ChangedItems = items.Where(static item => item.NeedsRename).ToArray();
    }

    public IReadOnlyList<RenamePlanItem> Items { get; }

    public IReadOnlyList<RenamePlanItem> ChangedItems { get; }

    public bool HasChanges => ChangedItems.Count > 0;
}

internal sealed class RenamePlanItem
{
    public RenamePlanItem(SelectedFile source, string newName)
    {
        Source = source;
        NewName = newName;
        TargetPath = Path.Combine(source.DirectoryPath, newName);
    }

    public SelectedFile Source { get; }

    public string NewName { get; }

    public string TargetPath { get; }

    public string? TemporaryPath { get; set; }

    public bool NeedsRename => !string.Equals(Source.Name, NewName, StringComparison.Ordinal);
}
