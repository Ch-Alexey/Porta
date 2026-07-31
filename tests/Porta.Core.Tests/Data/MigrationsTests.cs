using Porta.Core.Data;

namespace Porta.Core.Tests.Data;

public class MigrationsTests
{
    [Fact]
    public void Migrate_creates_expected_tables()
    {
        using var temp = new TempDatabase();
        using var connection = temp.Database.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));

        Assert.Contains("devices", tables);
        Assert.Contains("storages", tables);
        Assert.Contains("storage_devices", tables);
        Assert.Contains("settings", tables);
        Assert.Contains("file_index", tables);
    }

    [Fact]
    public void Migrate_is_idempotent()
    {
        using var temp = new TempDatabase();

        // Повторный вызов не должен падать и не должен ломать схему.
        temp.Database.Migrate();
        temp.Database.Migrate();

        using var connection = temp.Database.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        Assert.Equal((long)Migrations.All.Count, (long)cmd.ExecuteScalar()!);
    }
}
