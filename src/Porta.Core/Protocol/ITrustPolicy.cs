using Porta.Core.Identity;

namespace Porta.Core.Protocol;

/// <summary>
/// Политика доверия: доверяем ли устройству с данным Device ID. Позже — поверх
/// репозитория доверенных устройств. См. docs/features/07-protocol.md.
/// </summary>
public interface ITrustPolicy
{
    bool IsTrusted(DeviceId deviceId);
}
