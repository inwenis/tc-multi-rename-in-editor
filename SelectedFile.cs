namespace TcBatchRename;

internal sealed record SelectedFile(
    string FullPath,
    string DirectoryPath,
    string Name);
