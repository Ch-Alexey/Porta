using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Data;

/// <summary>Хранилище доверенных устройств (абстракция для тестируемости UI/логики).</summary>
public interface IDeviceRepository
{
    void Add(TrustedDevice device);
    TrustedDevice? Get(DeviceId id);
    IReadOnlyList<TrustedDevice> List();
    bool Remove(DeviceId id);
    void UpdateLastSeen(DeviceId id, DateTimeOffset when);
}
