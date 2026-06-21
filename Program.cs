using System.Diagnostics;

namespace TcBatchRename;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        string? tempNamesFilePath = null;

        try
        {
            var options = CommandLineOptions.Parse(args);

            if (options.ShowHelp)
            {
                Ui.ShowInfo("TC Batch Rename", UsageText);
                return 0;
            }

            string configPath = options.ConfigPath
                ?? Path.Combine(AppContext.BaseDirectory, AppConfig.DefaultFileName);

            var config = AppConfig.LoadOrCreate(configPath);
            var selectedFiles = FileListIo.LoadSelectedFiles(options);

            if (selectedFiles.Count == 0)
            {
                throw new InvalidOperationException("No files were provided. Total Commander should call this tool with `--list %UL`.");
            }

            IReadOnlyList<string> editedNames;

            if (options.EditedFilePath is not null)
            {
                editedNames = FileListIo.ReadLines(options.EditedFilePath);
            }
            else
            {
                tempNamesFilePath = CreateTempNamesFile(selectedFiles);
                LaunchEditor(config, configPath, tempNamesFilePath);
                editedNames = FileListIo.ReadLines(tempNamesFilePath);
            }

            var plan = RenamePlanner.Build(selectedFiles, editedNames);
            if (!plan.HasChanges)
            {
                return 0;
            }

            bool shouldConfirm = config.ConfirmBeforeRename && !options.SkipConfirmation;
            if (shouldConfirm && !Ui.ConfirmRename(plan))
            {
                return 0;
            }

            var succeeded = new List<RenamePlanItem>();
            var result = RenameExecutor.Execute(plan);
            succeeded.AddRange(result.Succeeded);

            while (result.HasFailures)
            {
                bool userChoseToRetry = Ui.ShowRenameResults(succeeded, result.Failed);
                if (!userChoseToRetry)
                {
                    // User left some files locked; report non-success to Total Commander.
                    return 1;
                }

                var retryPlan = new RenamePlan(result.Failed.Select(static failure => failure.Item).ToList());
                result = RenameExecutor.Execute(retryPlan);
                succeeded.AddRange(result.Succeeded);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Ui.ShowError("TC Batch Rename", ex.Message);
            return 1;
        }
        finally
        {
            if (tempNamesFilePath is not null)
            {
                TryDelete(tempNamesFilePath);
            }
        }
    }

    private static string CreateTempNamesFile(IReadOnlyList<SelectedFile> selectedFiles)
    {
        string tempPath = Path.Combine(
            Path.GetTempPath(),
            $"tc-batch-rename-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");

        FileListIo.WriteLines(tempPath, selectedFiles.Select(static file => file.Name));
        return tempPath;
    }

    private static void LaunchEditor(AppConfig config, string configPath, string tempNamesFilePath)
    {
        string editorPath = Environment.ExpandEnvironmentVariables(config.EditorPath);
        if (string.IsNullOrWhiteSpace(editorPath))
        {
            throw new InvalidOperationException($"Editor path is empty in configuration file:\r\n{configPath}");
        }

        var arguments = config.EditorArguments.Count == 0
            ? new List<string> { tempNamesFilePath }
            : new List<string>(config.EditorArguments.Count + 1);

        bool filePlaceholderFound = false;
        foreach (string rawArgument in config.EditorArguments)
        {
            string argument = Environment.ExpandEnvironmentVariables(rawArgument);
            if (argument.Contains(AppConfig.FilePlaceholder, StringComparison.Ordinal))
            {
                filePlaceholderFound = true;
                argument = argument.Replace(AppConfig.FilePlaceholder, tempNamesFilePath, StringComparison.Ordinal);
            }

            arguments.Add(argument);
        }

        if (!filePlaceholderFound)
        {
            arguments.Add(tempNamesFilePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = editorPath,
            UseShellExecute = false,
            WorkingDirectory = string.IsNullOrWhiteSpace(config.EditorWorkingDirectory)
                ? AppContext.BaseDirectory
                : Environment.ExpandEnvironmentVariables(config.EditorWorkingDirectory)
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Failed to launch the configured editor:\r\n{editorPath}");
        }

        process.WaitForExit();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private const string UsageText =
        "Usage:\r\n" +
        "  TcBatchRename.exe --list <total-commander-list-file>\r\n" +
        "  TcBatchRename.exe <file1> <file2> ...\r\n" +
        "\r\n" +
        "Recommended Total Commander parameters:\r\n" +
        "  --list \"%UL\"\r\n" +
        "\r\n" +
        "Optional arguments:\r\n" +
        "  --config <path>      Use a custom JSON config file.\r\n" +
        "  --edited-file <path> Read edited names from a file instead of launching an editor.\r\n" +
        "  --no-confirm         Skip the rename preview dialog.\r\n" +
        "  --help               Show this help.\r\n";
}
