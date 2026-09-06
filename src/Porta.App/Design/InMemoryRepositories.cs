using System;
using System.Collections.Generic;
using System.Linq;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.Design;

/// <summary>In-memory доверенные устройства — для тестов view-моделей и design-time.</summary>
public sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly Dictionary<string, TrustedDevice> _devices = new(StringComparer.Ordinal);

    public void Add(TrustedDevice device) => _devices[device.Id.ToString()] = device;

    public TrustedDevice? Get(DeviceId id) => _devices.GetValueOrDefault(id.ToString());

    public IReadOnlyList<TrustedDevice> List() => _devices.Values.OrderBy(d => d.AddedAt).ToList();

    public bool Remove(DeviceId id) => _devices.Remove(id.ToString());

    public void UpdateLastSeen(DeviceId id, DateTimeOffset when)
    {
        if (_devices.TryGetValue(id.ToString(), out TrustedDevice? device))
            _devices[id.ToString()] = device with { LastSeenAt = when };
    }
}

/// <summary>In-memory хранилища — для тестов view-моделей и design-time.</summary>
public sealed class InMemoryStorageRepository : IStorageRepository
{
    private readonly Dictionary<string, Storage> _storages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StorageDeviceLink> _links = new(StringComparer.Ordinal);

    private static string LinkKey(string storageId, DeviceId deviceId) => $"{storageId}|{deviceId}";

    public void Add(Storage storage) => _storages[storage.Id] = storage;

    public void Update(Storage storage)
    {
        if (_storages.ContainsKey(storage.Id))
            _storages[storage.Id] = storage;
    }

    public Storage? Get(string storageId) => _storages.GetValueOrDefault(storageId);

    public IReadOnlyList<Storage> List() => _storages.Values.OrderBy(s => s.CreatedAt).ToList();

    public bool Remove(string storageId)
    {
        foreach (string key in _links.Keys.Where(k => k.StartsWith(storageId + "|", StringComparison.Ordinal)).ToList())
            _links.Remove(key);
        return _storages.Remove(storageId);
    }

    public void LinkDevice(string storageId, DeviceId deviceId, bool autoSync)
        => _links[LinkKey(storageId, deviceId)] = new StorageDeviceLink(storageId, deviceId, autoSync);

    public bool UnlinkDevice(string storageId, DeviceId deviceId) => _links.Remove(LinkKey(storageId, deviceId));

    public IReadOnlyList<StorageDeviceLink> ListDevices(string storageId)
        => _links.Values.Where(l => l.StorageId == storageId).ToList();
}
