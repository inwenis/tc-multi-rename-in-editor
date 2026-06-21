using TcBatchRename;
using Xunit;

namespace TcBatchRename.Tests;

public sealed class RenameExecutorTests : IDisposable
{
    private readonly string _dir;

    public RenameExecutorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tc-batch-rename-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
        }
    }

    private string CreateFile(string name, string content = "x")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private RenamePlanItem Item(string oldName, string newName)
    {
        return new RenamePlanItem(new SelectedFile(Path.Combine(_dir, oldName), _dir, oldName), newName);
    }

    private static RenamePlan Plan(params RenamePlanItem[] items) => new(items);

    [Fact]
    public void RenamesAllFilesWhenNothingLocked()
    {
        CreateFile("a.txt");
        CreateFile("b.txt");
        var plan = Plan(Item("a.txt", "a-new.txt"), Item("b.txt", "b-new.txt"));

        RenameResult result = RenameExecutor.Execute(plan);

        Assert.Empty(result.Failed);
        Assert.Equal(2, result.Succeeded.Count);
        Assert.True(File.Exists(Path.Combine(_dir, "a-new.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "b-new.txt")));
        Assert.False(File.Exists(Path.Combine(_dir, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_dir, "b.txt")));
    }

    [Fact]
    public void RenamesUnlockedFilesAndReportsLockedOne()
    {
        CreateFile("a.txt");
        string lockedPath = CreateFile("locked.txt");
        CreateFile("b.txt");
        var plan = Plan(
            Item("a.txt", "a-new.txt"),
            Item("locked.txt", "locked-new.txt"),
            Item("b.txt", "b-new.txt"));

        using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            RenameResult result = RenameExecutor.Execute(plan);

            Assert.Equal(2, result.Succeeded.Count);
            RenameFailure failure = Assert.Single(result.Failed);
            Assert.Equal("locked.txt", failure.Item.Source.Name);
        }

        Assert.True(File.Exists(Path.Combine(_dir, "a-new.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "b-new.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "locked.txt")));
        Assert.False(File.Exists(Path.Combine(_dir, "locked-new.txt")));
    }

    [Fact]
    public void SwapWithOneSideLockedFailsBothAndRestoresOriginals()
    {
        string aPath = CreateFile("a.txt", "AAA");
        CreateFile("b.txt", "BBB");
        var plan = Plan(Item("a.txt", "b.txt"), Item("b.txt", "a.txt"));

        using (new FileStream(aPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            RenameResult result = RenameExecutor.Execute(plan);

            Assert.Empty(result.Succeeded);
            Assert.Equal(2, result.Failed.Count);
        }

        Assert.Equal("AAA", File.ReadAllText(Path.Combine(_dir, "a.txt")));
        Assert.Equal("BBB", File.ReadAllText(Path.Combine(_dir, "b.txt")));
    }

    [Fact]
    public void RetryAfterUnlockingCompletesTheRename()
    {
        string lockedPath = CreateFile("locked.txt");

        using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            RenameResult first = RenameExecutor.Execute(Plan(Item("locked.txt", "locked-new.txt")));
            Assert.Single(first.Failed);
        }

        RenameResult second = RenameExecutor.Execute(Plan(Item("locked.txt", "locked-new.txt")));

        Assert.Empty(second.Failed);
        Assert.Single(second.Succeeded);
        Assert.True(File.Exists(Path.Combine(_dir, "locked-new.txt")));
        Assert.False(File.Exists(Path.Combine(_dir, "locked.txt")));
    }

    [Fact]
    public void UnexpectedErrorStillThrows()
    {
        // Source file never created -> File.Move throws FileNotFoundException, which is
        // not a lock and must surface as a hard error rather than a retryable failure.
        var plan = Plan(Item("does-not-exist.txt", "whatever.txt"));

        Assert.Throws<InvalidOperationException>(() => RenameExecutor.Execute(plan));
    }
}
