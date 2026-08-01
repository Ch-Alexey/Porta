using Porta.Core.Data;
using Porta.Core.Identity;

namespace Porta.Core.App;

/// <summary>
/// Инициализация приложения: папка данных, личность устройства, БД и репозитории.
/// Платформонезависимо. Используется UI как единая точка доступа к ядру.
/// </summary>
public sealed class AppEnvironment
{
    private AppEnvironment(
        DeviceIdentity identity,
        string deviceName,
        PortaDatabase database,
        DeviceRepository devices,
        StorageRepository storages,
        FileIndexRepository fileIndex,
        string dataDirectory)
    {
        Identity = identity;
        DeviceName = deviceName;
        Database = database;
        Devices = devices;
        Storages = storages;
        FileIndex = fileIndex;
        DataDirectory = dataDirectory;
    }

    /// <summary>Личность этого устройства.</summary>
    public DeviceIdentity Identity { get; }

    /// <summary>Отображаемое имя устройства.</summary>
    public string DeviceName { get; }

    public PortaDatabase Database { get; }
    public DeviceRepository Devices { get; }
    public StorageRepository Storages { get; }
    public FileIndexRepository FileIndex { get; }

    /// <summary>Папка с данными приложения (ключ, БД).</summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Инициализировать окружение: создать/загрузить личность и БД в папке данных.
    /// </summary>
    public static AppEnvironment Create(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Porta");
        Directory.CreateDirectory(dataDirectory);

        DeviceIdentity identity = new FileDeviceIdentityStore(Path.Combine(dataDirectory, "device.key")).LoadOrCreate();

        var database = new PortaDatabase(Path.Combine(dataDirectory, "porta.db"));
        database.Migrate();

        return new AppEnvironment(
            identity,
            Environment.MachineName,
            database,
            new DeviceRepository(database),
            new StorageRepository(database),
            new FileIndexRepository(database),
            dataDirectory);
    }
}
