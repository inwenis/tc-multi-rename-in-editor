using System.Text;
using System.Text.Json;

namespace TcBatchRename;

internal sealed class AppConfig
{
    public const string DefaultFileName = "tc-batch-rename.json";
    public const string FilePlaceholder = "{file}";

    public string EditorPath { get; init; } = "notepad.exe";

    public List<string> EditorArguments { get; init; } = new() { FilePlaceholder };

    public string EditorWorkingDirectory { get; init; } = string.Empty;

    public bool ConfirmBeforeRename { get; init; } = true;

    public static AppConfig LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = new AppConfig();
            string json = JsonSerializer.Serialize(defaultConfig, SerializerOptions);
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            var config = JsonSerializer.Deserialize<AppConfig>(json, DeserializerOptions);
            if (config is null)
            {
                throw new InvalidOperationException("The configuration file is empty.");
            }

            return config;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Failed to load configuration file:\r\n{path}\r\n\r\n{ex.Message}");
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
