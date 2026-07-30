using Porta.Core.Data;

namespace Porta.Core.Tests.Data;

/// <summary>
/// Одноразовая файловая БД во временной папке: создаётся мигрированной, чистится в Dispose.
/// </summary>
internal sealed class TempDatabase : IDisposable
{
    private readonly string _dir;

    public TempDatabase()
    {
        _dir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        Database = new PortaDatabase(Path.Combine(_dir, "porta.db"));
        Database.Migrate();
    }

    public PortaDatabase Database { get; }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
