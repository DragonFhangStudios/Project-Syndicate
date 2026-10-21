namespace DFS.ProjectSyndicate.Tests;

// A unique, empty save directory per test, removed afterwards.
public sealed class TempSaveDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "syndicate-tests", Guid.NewGuid().ToString("N"));

    public TempSaveDirectory() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}
