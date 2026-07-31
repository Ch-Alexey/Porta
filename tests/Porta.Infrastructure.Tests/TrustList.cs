using Porta.Core.Identity;
using Porta.Core.Protocol;

namespace Porta.Infrastructure.Tests;

/// <summary>Тестовая политика доверия по явному списку Device ID.</summary>
internal sealed class TrustList : ITrustPolicy
{
    private readonly HashSet<string> _trusted;

    public TrustList(params DeviceId[] trusted)
        => _trusted = trusted.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);

    public bool IsTrusted(DeviceId deviceId) => _trusted.Contains(deviceId.ToString());
}
