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

internal sealed class RenameResult
{
    public RenameResult(IReadOnlyList<RenamePlanItem> succeeded, IReadOnlyList<RenameFailure> failed)
    {
        Succeeded = succeeded;
        Failed = failed;
    }

    public IReadOnlyList<RenamePlanItem> Succeeded { get; }

    public IReadOnlyList<RenameFailure> Failed { get; }

    public bool HasFailures => Failed.Count > 0;
}

internal sealed class RenameFailure
{
    public RenameFailure(RenamePlanItem item, string reason)
    {
        Item = item;
        Reason = reason;
    }

    public RenamePlanItem Item { get; }

    public string Reason { get; }
}
