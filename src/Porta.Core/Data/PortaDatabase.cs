using Microsoft.Data.Sqlite;

namespace Porta.Core.Data;

/// <summary>
/// Точка доступа к локальной БД метаданных. Открывает настроенные соединения
/// (WAL + foreign_keys) и применяет миграции схемы. См. ADR-0007.
/// </summary>
public sealed class PortaDatabase
{
    private readonly string _connectionString;

    /// <param name="databasePath">Путь к файлу БД SQLite.</param>
    public PortaDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
        }.ToString();
    }

    /// <summary>Открыть соединение с включёнными WAL и внешними ключами.</summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        // journal_mode — свойство БД (сохраняется), foreign_keys — на каждое соединение.
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    /// <summary>Привести схему к актуальной версии (идемпотентно).</summary>
    public void Migrate()
    {
        using var connection = OpenConnection();

        long applied = GetUserVersion(connection);
        var migrations = Migrations.All;
        if (applied >= migrations.Count)
            return;

        using var transaction = connection.BeginTransaction();
        for (int version = (int)applied; version < migrations.Count; version++)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = migrations[version];
            cmd.ExecuteNonQuery();
        }

        SetUserVersion(connection, transaction, migrations.Count);
        transaction.Commit();
    }

    private static long GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    private static void SetUserVersion(SqliteConnection connection, SqliteTransaction transaction, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        // PRAGMA не принимает параметры; version — наш внутренний int, инъекции нет.
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }
}
