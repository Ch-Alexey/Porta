using Microsoft.Data.Sqlite;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Data;

/// <summary>Хранилище доверенных устройств. См. docs/features/02-data-model.md.</summary>
public sealed class DeviceRepository
{
    private readonly PortaDatabase _db;

    public DeviceRepository(PortaDatabase db) => _db = db;

    public void Add(TrustedDevice device)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO devices (device_id, public_key, name, added_at, last_seen_at)
            VALUES ($id, $pk, $name, $added, $seen);
            """;
        cmd.Parameters.AddWithValue("$id", device.Id.ToString());
        cmd.Parameters.AddWithValue("$pk", device.PublicKey);
        cmd.Parameters.AddWithValue("$name", device.Name);
        cmd.Parameters.AddWithValue("$added", device.AddedAt.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$seen", (object?)device.LastSeenAt?.ToUnixTimeSeconds() ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public TrustedDevice? Get(DeviceId id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT device_id, public_key, name, added_at, last_seen_at FROM devices WHERE device_id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public IReadOnlyList<TrustedDevice> List()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT device_id, public_key, name, added_at, last_seen_at FROM devices ORDER BY added_at;";
        using var reader = cmd.ExecuteReader();
        var result = new List<TrustedDevice>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    /// <returns><c>true</c>, если устройство существовало и было удалено.</returns>
    public bool Remove(DeviceId id)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM devices WHERE device_id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }

    public void UpdateLastSeen(DeviceId id, DateTimeOffset when)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE devices SET last_seen_at = $seen WHERE device_id = $id;";
        cmd.Parameters.AddWithValue("$seen", when.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    private static TrustedDevice Map(SqliteDataReader reader) => new(
        DeviceId.Parse(reader.GetString(0)),
        reader.GetFieldValue<byte[]>(1),
        reader.GetString(2),
        DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
        reader.IsDBNull(4) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)));
}
