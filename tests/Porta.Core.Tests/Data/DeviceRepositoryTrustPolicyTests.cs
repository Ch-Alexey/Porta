using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Protocol;

namespace Porta.Core.Tests.Data;

public class DeviceRepositoryTrustPolicyTests
{
    [Fact]
    public void Trusts_device_present_in_repository()
    {
        using var temp = new TempDatabase();
        var repo = new DeviceRepository(temp.Database);
        using var known = DeviceIdentity.Generate();
        using var stranger = DeviceIdentity.Generate();
        repo.Add(new TrustedDevice(known.Id, known.ExportPublicKey(), "Known",
            DateTimeOffset.UnixEpoch, null));

        ITrustPolicy policy = new DeviceRepositoryTrustPolicy(repo);

        Assert.True(policy.IsTrusted(known.Id));
        Assert.False(policy.IsTrusted(stranger.Id));
    }
}
