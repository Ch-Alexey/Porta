using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Sync;

/// <summary>
/// Какие хранилища синхронизировать с устройством по данному поводу.
/// См. docs/features/29-managing-what-exists.md.
/// </summary>
public static class StorageSelection
{
    /// <summary>
    /// Расшаренные с устройством хранилища, отфильтрованные по поводу:
    /// приостановленные не берём никогда, ручные — только когда нажал человек.
    /// </summary>
    public static IReadOnlyList<Storage> ForSync(
        IStorageRepository storages,
        DeviceId device,
        SyncTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(storages);

        var result = new List<Storage>();
        foreach (Storage storage in StorageSharing.SharedWith(storages, device))
        {
            if (storage.Paused)
                continue;
            if (trigger == SyncTrigger.Automatic && storage.SyncMode != SyncMode.Automatic)
                continue;
            result.Add(storage);
        }

        return result;
    }
}
