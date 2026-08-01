using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Data;

/// <summary>Хранилище записей о папках синхронизации (абстракция для тестируемости).</summary>
public interface IStorageRepository
{
    void Add(Storage storage);
    Storage? Get(string storageId);
    IReadOnlyList<Storage> List();
    bool Remove(string storageId);
    void LinkDevice(string storageId, DeviceId deviceId, bool autoSync);
    bool UnlinkDevice(string storageId, DeviceId deviceId);
    IReadOnlyList<StorageDeviceLink> ListDevices(string storageId);
}
