using MessagePack;
using Porta.Core.Sync;

namespace Porta.Core.Data;

/// <summary>
/// Персистентность версионированного индекса хранилища: запись = MessagePack(VersionedFileEntry).
/// Вектора версий переживают перезапуск. См. docs/features/17-versioned-sync.md.
/// </summary>
public sealed class FileIndexRepository
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard;

    private readonly PortaDatabase _db;

    public FileIndexRepository(PortaDatabase db) => _db = db;

    /// <summary>Загрузить версионированный индекс хранилища (по возрастанию пути).</summary>
    public IReadOnlyList<VersionedFileEntry> Load(string storageId)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT entry FROM file_index WHERE storage_id = $id ORDER BY relative_path;";
        cmd.Parameters.AddWithValue("$id", storageId);

        using var reader = cmd.ExecuteReader();
        var result = new List<VersionedFileEntry>();
        while (reader.Read())
            result.Add(MessagePackSerializer.Deserialize<VersionedFileEntry>(reader.GetFieldValue<byte[]>(0), Options));
        return result;
    }

    /// <summary>Полностью заменить индекс хранилища на переданный (в одной транзакции).</summary>
    public void Replace(string storageId, IReadOnlyList<VersionedFileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        using var connection = _db.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM file_index WHERE storage_id = $id;";
            delete.Parameters.AddWithValue("$id", storageId);
            delete.ExecuteNonQuery();
        }

        foreach (VersionedFileEntry entry in entries)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO file_index (storage_id, relative_path, content_hash, entry)
                VALUES ($id, $path, $hash, $entry);
                """;
            insert.Parameters.AddWithValue("$id", storageId);
            insert.Parameters.AddWithValue("$path", entry.Entry.RelativePath);
            insert.Parameters.AddWithValue("$hash", entry.Entry.ContentHash);
            insert.Parameters.AddWithValue("$entry", MessagePackSerializer.Serialize(entry, Options));
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
