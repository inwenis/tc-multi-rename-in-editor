namespace TcBatchRename;

internal static class RenamePlanner
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    };

    public static RenamePlan Build(IReadOnlyList<SelectedFile> selectedFiles, IReadOnlyList<string> editedNames)
    {
        if (selectedFiles.Count != editedNames.Count)
        {
            throw new InvalidOperationException(
                $"The edited file contains {editedNames.Count} line(s), but {selectedFiles.Count} file(s) were selected.\r\n\r\n" +
                "The number of lines must stay exactly the same. No files were renamed.");
        }

        var items = new List<RenamePlanItem>(selectedFiles.Count);
        for (int index = 0; index < selectedFiles.Count; index++)
        {
            string editedName = editedNames[index];
            if (editedName.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Line {index + 1} is empty. Aborting without renaming anything.");
            }

            ValidateFileName(index + 1, editedName);
            items.Add(new RenamePlanItem(selectedFiles[index], editedName));
        }

        ValidateDuplicateTargets(items);
        ValidateExistingConflicts(items);

        return new RenamePlan(items);
    }

    private static void ValidateFileName(int lineNumber, string fileName)
    {
        if (fileName.EndsWith(' ') || fileName.EndsWith('.'))
        {
            throw new InvalidOperationException(
                $"Line {lineNumber} ends with a space or period, which is not a valid Windows file name:\r\n{fileName}");
        }

        if (fileName is "." or "..")
        {
            throw new InvalidOperationException(
                $"Line {lineNumber} is not a valid file name:\r\n{fileName}");
        }

        if (Path.IsPathRooted(fileName)
            || fileName.Contains(Path.DirectorySeparatorChar)
            || fileName.Contains(Path.AltDirectorySeparatorChar)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Line {lineNumber} contains a path. Only the file name may be edited:\r\n{fileName}");
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException(
                $"Line {lineNumber} contains characters that are not valid in a Windows file name:\r\n{fileName}");
        }

        string baseName = fileName.Split('.')[0];
        if (ReservedDeviceNames.Contains(baseName))
        {
            throw new InvalidOperationException(
                $"Line {lineNumber} uses a reserved Windows device name:\r\n{fileName}");
        }
    }

    private static void ValidateDuplicateTargets(IEnumerable<RenamePlanItem> items)
    {
        var seenTargets = new Dictionary<string, RenamePlanItem>(StringComparer.OrdinalIgnoreCase);
        foreach (RenamePlanItem item in items)
        {
            if (seenTargets.TryGetValue(item.TargetPath, out RenamePlanItem? existing))
            {
                throw new InvalidOperationException(
                    "Two or more files would end up with the same target name in the same directory.\r\n\r\n" +
                    $"First:  {existing.Source.FullPath} -> {existing.NewName}\r\n" +
                    $"Second: {item.Source.FullPath} -> {item.NewName}\r\n\r\n" +
                    "No files were renamed.");
            }

            seenTargets[item.TargetPath] = item;
        }
    }

    private static void ValidateExistingConflicts(IEnumerable<RenamePlanItem> items)
    {
        var selectedPaths = new HashSet<string>(
            items.Select(static item => item.Source.FullPath),
            StringComparer.OrdinalIgnoreCase);

        foreach (RenamePlanItem item in items)
        {
            if (!item.NeedsRename)
            {
                continue;
            }

            if (File.Exists(item.TargetPath) && !selectedPaths.Contains(item.TargetPath))
            {
                throw new InvalidOperationException(
                    $"The target file already exists and is not part of the current selection:\r\n{item.TargetPath}\r\n\r\n" +
                    "No files were renamed.");
            }

            if (Directory.Exists(item.TargetPath))
            {
                throw new InvalidOperationException(
                    $"A directory already exists with the target name:\r\n{item.TargetPath}\r\n\r\n" +
                    "No files were renamed.");
            }
        }
    }
}
