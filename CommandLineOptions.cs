namespace TcBatchRename;

internal sealed class CommandLineOptions
{
    public bool ShowHelp { get; init; }

    public string? ListFilePath { get; init; }

    public string? ConfigPath { get; init; }

    public string? EditedFilePath { get; init; }

    public bool SkipConfirmation { get; init; }

    public IReadOnlyList<string> DirectPaths { get; init; } = Array.Empty<string>();

    public static CommandLineOptions Parse(string[] args)
    {
        var directPaths = new List<string>();
        string? listFilePath = null;
        string? configPath = null;
        string? editedFilePath = null;
        bool skipConfirmation = false;
        bool showHelp = false;

        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            switch (arg)
            {
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;

                case "--list":
                    listFilePath = ReadValue(args, ref index, arg);
                    break;

                case "--config":
                    configPath = ReadValue(args, ref index, arg);
                    break;

                case "--edited-file":
                    editedFilePath = ReadValue(args, ref index, arg);
                    break;

                case "--no-confirm":
                    skipConfirmation = true;
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Unknown argument: {arg}");
                    }

                    directPaths.Add(arg);
                    break;
            }
        }

        if (listFilePath is not null && directPaths.Count > 0)
        {
            throw new InvalidOperationException("Use either `--list <file>` or direct file paths, not both.");
        }

        return new CommandLineOptions
        {
            ShowHelp = showHelp,
            ListFilePath = listFilePath,
            ConfigPath = configPath,
            EditedFilePath = editedFilePath,
            SkipConfirmation = skipConfirmation,
            DirectPaths = directPaths
        };
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }
}
