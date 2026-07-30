using Microsoft.Data.Sqlite;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Data;

/// <summary>Хранилище записей о папках синхронизации и их привязке к устройствам.</summary>
public sealed class StorageRepository
{
    private readonly PortaDatabase _db;

    public StorageRepository(PortaDatabase db) => _db = db;

    public void Add(Storage storage)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO storages (storage_id, name, local_path, exchange_mode, sync_mode, paused, created_at)
            VALUES ($id, $name, $path, $exchange, $sync, $paused, $created);
            """;
        cmd.Parameters.AddWithValue("$id", storage.Id);
        cmd.Parameters.AddWithValue("$name", storage.Name);
        cmd.Parameters.AddWithValue("$path", storage.LocalPath);
        cmd.Parameters.AddWithValue("$exchange", (int)storage.ExchangeMode);
        cmd.Parameters.AddWithValue("$sync", (int)storage.SyncMode);
        cmd.Parameters.AddWithValue("$paused", storage.Paused ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", storage.CreatedAt.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    public Storage? Get(string storageId)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT storage_id, name, local_path, exchange_mode, sync_mode, paused, created_at FROM storages WHERE storage_id = $id;";
        cmd.Parameters.AddWithValue("$id", storageId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public IReadOnlyList<Storage> List()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT storage_id, name, local_path, exchange_mode, sync_mode, paused, created_at FROM storages ORDER BY created_at;";
        using var reader = cmd.ExecuteReader();
        var result = new List<Storage>();
        while (reader.Read())
            result.Add(Map(reader));
        return result;
    }

    /// <returns><c>true</c>, если хранилище существовало и было удалено (вместе со связями).</returns>
    public bool Remove(string storageId)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM storages WHERE storage_id = $id;";
        cmd.Parameters.AddWithValue("$id", storageId);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>Привязать устройство к хранилищу (или обновить флаг авто-синхро).</summary>
    public void LinkDevice(string storageId, DeviceId deviceId, bool autoSync)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO storage_devices (storage_id, device_id, auto_sync)
            VALUES ($storage, $device, $auto)
            ON CONFLICT (storage_id, device_id) DO UPDATE SET auto_sync = excluded.auto_sync;
            """;
        cmd.Parameters.AddWithValue("$storage", storageId);
        cmd.Parameters.AddWithValue("$device", deviceId.ToString());
        cmd.Parameters.AddWithValue("$auto", autoSync ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public bool UnlinkDevice(string storageId, DeviceId deviceId)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM storage_devices WHERE storage_id = $storage AND device_id = $device;";
        cmd.Parameters.AddWithValue("$storage", storageId);
        cmd.Parameters.AddWithValue("$device", deviceId.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<StorageDeviceLink> ListDevices(string storageId)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT storage_id, device_id, auto_sync FROM storage_devices WHERE storage_id = $storage;";
        cmd.Parameters.AddWithValue("$storage", storageId);
        using var reader = cmd.ExecuteReader();
        var result = new List<StorageDeviceLink>();
        while (reader.Read())
        {
            result.Add(new StorageDeviceLink(
                reader.GetString(0),
                DeviceId.Parse(reader.GetString(1)),
                reader.GetInt64(2) != 0));
        }
        return result;
    }

    private static Storage Map(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        (StorageExchangeMode)reader.GetInt64(3),
        (SyncMode)reader.GetInt64(4),
        reader.GetInt64(5) != 0,
        DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)));
}
