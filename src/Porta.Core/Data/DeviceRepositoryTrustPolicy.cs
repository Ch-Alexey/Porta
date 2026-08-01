using Porta.Core.Identity;
using Porta.Core.Protocol;

namespace Porta.Core.Data;

/// <summary>
/// Политика доверия поверх репозитория устройств: доверяем тем, кто есть в БД доверенных.
/// Закрывает связку `ITrustPolicy` ↔ `DeviceRepository`. См. docs/features/07-protocol.md.
/// </summary>
public sealed class DeviceRepositoryTrustPolicy(IDeviceRepository devices) : ITrustPolicy
{
    public bool IsTrusted(DeviceId deviceId) => devices.Get(deviceId) is not null;
}
