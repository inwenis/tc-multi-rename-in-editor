namespace TcBatchRename;

internal static class RenameExecutor
{
    public static void Execute(RenamePlan plan)
    {
        var tempMoved = new List<RenamePlanItem>(plan.ChangedItems.Count);
        var finalMoved = new List<RenamePlanItem>(plan.ChangedItems.Count);
        RenamePlanItem? failingItem = null;
        string? fromPath = null;
        string? toPath = null;
        string phase = "preparation";

        try
        {
            foreach (RenamePlanItem item in plan.ChangedItems)
            {
                item.TemporaryPath = CreateTemporaryPath(item.Source.DirectoryPath, item.Source.Name);
            }

            phase = "temporary rename";
            foreach (RenamePlanItem item in plan.ChangedItems)
            {
                failingItem = item;
                fromPath = item.Source.FullPath;
                toPath = item.TemporaryPath;
                File.Move(item.Source.FullPath, item.TemporaryPath!);
                tempMoved.Add(item);
            }

            phase = "final rename";
            foreach (RenamePlanItem item in plan.ChangedItems)
            {
                failingItem = item;
                fromPath = item.TemporaryPath;
                toPath = item.TargetPath;
                File.Move(item.TemporaryPath!, item.TargetPath);
                finalMoved.Add(item);
            }
        }
        catch (Exception ex)
        {
            string rollbackMessage = RollBack(tempMoved, finalMoved);
            throw new InvalidOperationException(
                $"Rename failed during {phase}.\r\n\r\n" +
                $"From: {fromPath ?? failingItem?.Source.FullPath ?? "(unknown)"}\r\n" +
                $"To:   {toPath ?? failingItem?.TargetPath ?? "(unknown)"}\r\n\r\n" +
                $"{ex.Message}\r\n\r\n" +
                rollbackMessage);
        }
    }

    private static string RollBack(
        IReadOnlyList<RenamePlanItem> tempMoved,
        IReadOnlyList<RenamePlanItem> finalMoved)
    {
        var rollbackErrors = new List<string>();
        var finalMovedSet = new HashSet<RenamePlanItem>(finalMoved);

        for (int index = finalMoved.Count - 1; index >= 0; index--)
        {
            RenamePlanItem item = finalMoved[index];
            try
            {
                File.Move(item.TargetPath, item.Source.FullPath);
            }
            catch (Exception ex)
            {
                rollbackErrors.Add(
                    $"Failed to roll back {item.TargetPath} -> {item.Source.FullPath}: {ex.Message}");
            }
        }

        for (int index = tempMoved.Count - 1; index >= 0; index--)
        {
            RenamePlanItem item = tempMoved[index];
            if (finalMovedSet.Contains(item))
            {
                continue;
            }

            try
            {
                File.Move(item.TemporaryPath!, item.Source.FullPath);
            }
            catch (Exception ex)
            {
                rollbackErrors.Add(
                    $"Failed to roll back {item.TemporaryPath} -> {item.Source.FullPath}: {ex.Message}");
            }
        }

        return rollbackErrors.Count == 0
            ? "Rollback completed. Files were restored to their original names."
            : "Rollback was attempted, but some files could not be restored:\r\n\r\n" + string.Join("\r\n", rollbackErrors);
    }

    private static string CreateTemporaryPath(string directoryPath, string originalName)
    {
        string extension = Path.GetExtension(originalName);
        const string Prefix = "__tc_batch_rename__";

        while (true)
        {
            string tempName = $"{Prefix}{Guid.NewGuid():N}{extension}";
            string tempPath = Path.Combine(directoryPath, tempName);
            if (!File.Exists(tempPath) && !Directory.Exists(tempPath))
            {
                return tempPath;
            }
        }
    }
}
