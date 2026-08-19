using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Sync;

/// <summary>
/// Какие хранилища расшарены устройству (связаны в `storage_devices`). Синк касается только
/// их — выборочный доступ вместо «доверяешь → делишь всё». См. docs/features/24-storage-sharing.md.
/// </summary>
public static class StorageSharing
{
    public static IReadOnlyList<Storage> SharedWith(IStorageRepository storages, DeviceId device)
    {
        ArgumentNullException.ThrowIfNull(storages);
        ArgumentNullException.ThrowIfNull(device);

        var result = new List<Storage>();
        foreach (Storage storage in storages.List())
            if (storages.ListDevices(storage.Id).Any(link => link.DeviceId == device))
                result.Add(storage);
        return result;
    }
}
