namespace Porta.Core.Data;

/// <summary>
/// Упорядоченный список миграций схемы. Индекс в списке = номер версии (после
/// применения всех N миграций <c>PRAGMA user_version = N</c>). Только вперёд, аддитивно,
/// номера не переиспользуем. См. ADR-0007.
/// </summary>
internal static class Migrations
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        // 001 — базовая схема: устройства, хранилища, связь, настройки.
        """
        CREATE TABLE devices (
            device_id     TEXT    NOT NULL PRIMARY KEY,
            public_key    BLOB    NOT NULL,
            name          TEXT    NOT NULL,
            added_at      INTEGER NOT NULL,
            last_seen_at  INTEGER NULL
        );

        CREATE TABLE storages (
            storage_id    TEXT    NOT NULL PRIMARY KEY,
            name          TEXT    NOT NULL,
            local_path    TEXT    NOT NULL,
            exchange_mode INTEGER NOT NULL,
            sync_mode     INTEGER NOT NULL,
            paused        INTEGER NOT NULL,
            created_at    INTEGER NOT NULL
        );

        CREATE TABLE storage_devices (
            storage_id    TEXT    NOT NULL,
            device_id     TEXT    NOT NULL,
            auto_sync     INTEGER NOT NULL,
            PRIMARY KEY (storage_id, device_id),
            FOREIGN KEY (storage_id) REFERENCES storages (storage_id) ON DELETE CASCADE,
            FOREIGN KEY (device_id)  REFERENCES devices  (device_id)  ON DELETE CASCADE
        );

        CREATE TABLE settings (
            key           TEXT    NOT NULL PRIMARY KEY,
            value         TEXT    NOT NULL
        );
        """,

        // 002 — версионированный индекс файлов (запись = MessagePack(VersionedFileEntry)).
        """
        CREATE TABLE file_index (
            storage_id    TEXT NOT NULL,
            relative_path TEXT NOT NULL,
            content_hash  BLOB NOT NULL,
            entry         BLOB NOT NULL,
            PRIMARY KEY (storage_id, relative_path)
        );

        CREATE INDEX ix_file_index_storage ON file_index (storage_id);
        """,
    };
}
