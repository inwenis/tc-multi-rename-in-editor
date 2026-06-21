namespace TcBatchRename;

internal static class RenameExecutor
{
    public static RenameResult Execute(RenamePlan plan)
    {
        foreach (RenamePlanItem item in plan.ChangedItems)
        {
            item.TemporaryPath = CreateTemporaryPath(item.Source.DirectoryPath, item.Source.Name);
        }

        var failed = new List<RenameFailure>();
        var tempMoved = new List<RenamePlanItem>(plan.ChangedItems.Count);

        // Phase 1: move every changed file to a unique temporary name. A file held
        // open by another process is recorded as failed and skipped; the rest go on.
        foreach (RenamePlanItem item in plan.ChangedItems)
        {
            try
            {
                File.Move(item.Source.FullPath, item.TemporaryPath!);
                tempMoved.Add(item);
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                failed.Add(new RenameFailure(item, DescribeFailure(ex)));
            }
            catch (Exception ex)
            {
                RestoreAllFromTemp(tempMoved);
                throw HardError("temporary rename", item.Source.FullPath, item.TemporaryPath, ex);
            }
        }

        // Phase 2: move each temporary file to its final target. A target still
        // occupied by a locked file (for example the other half of a swap) fails;
        // that item is restored to its original name and reported as failed.
        var succeeded = new List<RenamePlanItem>(tempMoved.Count);
        foreach (RenamePlanItem item in tempMoved)
        {
            try
            {
                File.Move(item.TemporaryPath!, item.TargetPath);
                succeeded.Add(item);
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                SafeMove(item.TemporaryPath!, item.Source.FullPath);
                failed.Add(new RenameFailure(item, DescribeFailure(ex)));
            }
            catch (Exception ex)
            {
                RestoreAllFromTarget(succeeded);
                RestoreAllFromTemp(tempMoved.Where(moved => !succeeded.Contains(moved)));
                throw HardError("final rename", item.TemporaryPath, item.TargetPath, ex);
            }
        }

        return new RenameResult(succeeded, failed);
    }

    private static bool IsRetryable(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            return true;
        }

        if (ex is IOException)
        {
            return (ex.HResult & 0xFFFF) switch
            {
                0x20 => true, // ERROR_SHARING_VIOLATION
                0x21 => true, // ERROR_LOCK_VIOLATION
                0x05 => true, // ERROR_ACCESS_DENIED
                0x50 => true, // ERROR_FILE_EXISTS
                0xB7 => true, // ERROR_ALREADY_EXISTS
                _ => false,
            };
        }

        return false;
    }

    private static string DescribeFailure(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => "Access denied (file may be open elsewhere or read-only).",
            IOException io when (io.HResult & 0xFFFF) is 0x50 or 0xB7
                => "Target name is blocked by another locked file.",
            _ => "Locked or in use by another process.",
        };
    }

    private static void RestoreAllFromTemp(IEnumerable<RenamePlanItem> tempMoved)
    {
        foreach (RenamePlanItem item in tempMoved)
        {
            SafeMove(item.TemporaryPath!, item.Source.FullPath);
        }
    }

    private static void RestoreAllFromTarget(IEnumerable<RenamePlanItem> finalMoved)
    {
        foreach (RenamePlanItem item in finalMoved)
        {
            SafeMove(item.TargetPath, item.Source.FullPath);
        }
    }

    private static void SafeMove(string from, string to)
    {
        try
        {
            File.Move(from, to);
        }
        catch
        {
        }
    }

    private static InvalidOperationException HardError(string phase, string? fromPath, string? toPath, Exception ex)
    {
        return new InvalidOperationException(
            $"Rename failed during {phase}.\r\n\r\n" +
            $"From: {fromPath ?? "(unknown)"}\r\n" +
            $"To:   {toPath ?? "(unknown)"}\r\n\r\n" +
            $"{ex.Message}\r\n\r\n" +
            "The tool attempted to restore the other files to their original names.");
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
