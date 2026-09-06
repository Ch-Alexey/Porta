using Microsoft.Data.Sqlite;

namespace Porta.Core.Data;

/// <summary>Настройки приложения «ключ → значение». См. docs/features/28-drop-ui.md.</summary>
public interface ISettingsRepository
{
    /// <summary>Значение или <paramref name="fallback"/>, если ключа нет.</summary>
    string Get(string key, string fallback);

    /// <summary>Записать значение (перезаписывает существующее).</summary>
    void Set(string key, string value);
}

/// <summary>Известные ключи настроек — чтобы не разъезжались строки по коду.</summary>
public static class SettingKeys
{
    /// <summary>Папка, куда складываются принятые разовые передачи.</summary>
    public const string DownloadsFolder = "downloads.folder";
}

/// <summary>
/// Настройки в таблице `settings` (заведена миграцией 001, до сих пор не использовалась).
/// </summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private readonly PortaDatabase _db;

    public SettingsRepository(PortaDatabase db) => _db = db;

    public string Get(string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string ?? fallback;
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($key, $value)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }
}
