using System.Text;

namespace TcBatchRename;

internal static class FileListIo
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static IReadOnlyList<SelectedFile> LoadSelectedFiles(CommandLineOptions options)
    {
        IReadOnlyList<string> paths = options.ListFilePath is not null
            ? ReadLines(options.ListFilePath)
            : options.DirectPaths;

        var files = new List<SelectedFile>(paths.Count);
        foreach (string rawPath in paths)
        {
            string path = NormalizePath(rawPath);
            if (path.Length == 0)
            {
                continue;
            }

            if (!File.Exists(path))
            {
                if (Directory.Exists(path))
                {
                    throw new InvalidOperationException($"Directories are not supported by this helper.\r\n\r\nUnsupported item:\r\n{path}");
                }

                throw new InvalidOperationException($"The selected file no longer exists:\r\n{path}");
            }

            string directoryPath = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException($"Unable to determine the parent directory for:\r\n{path}");

            files.Add(new SelectedFile(path, directoryPath, Path.GetFileName(path)));
        }

        return files;
    }

    public static void WriteLines(string path, IEnumerable<string> lines)
    {
        string content = string.Join(Environment.NewLine, lines);
        File.WriteAllText(path, content, Utf8WithBom);
    }

    public static IReadOnlyList<string> ReadLines(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"File not found:\r\n{path}");
        }

        byte[] bytes = File.ReadAllBytes(path);
        string text = DecodeText(bytes);
        return SplitLines(text);
    }

    private static string NormalizePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        if (fullPath.Length > root.Length)
        {
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return fullPath;
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return Utf8WithoutBom.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Default.GetString(bytes);
        }
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
