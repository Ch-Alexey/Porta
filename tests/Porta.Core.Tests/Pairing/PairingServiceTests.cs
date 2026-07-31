using Porta.Core.Identity;
using Porta.Core.Pairing;

namespace Porta.Core.Tests.Pairing;

public class PairingServiceTests
{
    private readonly MutableTimeProvider _clock = new();
    private readonly PairingService _service;

    public PairingServiceTests() => _service = new PairingService(_clock);

    [Fact]
    public void Full_pairing_flow_succeeds_and_trusts_joiner()
    {
        using var inviter = DeviceIdentity.Generate();
        using var joiner = DeviceIdentity.Generate();

        PairingToken token = _service.CreateInvitation(inviter, ["192.168.0.2:1234"], TimeSpan.FromMinutes(5));
        PairingResponse response = _service.CreateProof(joiner, "Joiner Phone", token);
        PairingResult result = _service.VerifyProof(token, response);

        Assert.True(result.IsSuccess);
        Assert.Equal(joiner.Id, result.Device!.Id);
        Assert.Equal(joiner.ExportPublicKey(), result.Device.PublicKey);
        Assert.Equal("Joiner Phone", result.Device.Name);
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        using var inviter = DeviceIdentity.Generate();
        using var joiner = DeviceIdentity.Generate();
        PairingToken token = _service.CreateInvitation(inviter, ["a:1"], TimeSpan.FromMinutes(1));
        PairingResponse response = _service.CreateProof(joiner, "J", token);

        _clock.Advance(TimeSpan.FromMinutes(2));
        PairingResult result = _service.VerifyProof(token, response);

        Assert.Equal(PairingOutcome.Expired, result.Outcome);
    }

    [Fact]
    public void Unsupported_version_is_rejected()
    {
        using var inviter = DeviceIdentity.Generate();
        using var joiner = DeviceIdentity.Generate();
        PairingToken token = _service.CreateInvitation(inviter, ["a:1"], TimeSpan.FromMinutes(5));
        PairingResponse response = _service.CreateProof(joiner, "J", token);
        var futureToken = token with { Version = 999 };

        Assert.Equal(PairingOutcome.UnsupportedVersion, _service.VerifyProof(futureToken, response).Outcome);
    }

    [Fact]
    public void Wrong_secret_produces_bad_signature()
    {
        using var inviter = DeviceIdentity.Generate();
        using var joiner = DeviceIdentity.Generate();
        PairingToken token = _service.CreateInvitation(inviter, ["a:1"], TimeSpan.FromMinutes(5));

        // joiner доказывает по НЕВЕРНОМУ коду (атакующий не знает секрет из QR).
        var forgedToken = token with { Secret = new byte[PairingService.SecretSize] };
        PairingResponse response = _service.CreateProof(joiner, "J", forgedToken);

        Assert.Equal(PairingOutcome.BadSignature, _service.VerifyProof(token, response).Outcome);
    }

    [Fact]
    public void Identity_mismatch_when_id_does_not_match_key()
    {
        using var inviter = DeviceIdentity.Generate();
        using var joiner = DeviceIdentity.Generate();
        using var other = DeviceIdentity.Generate();
        PairingToken token = _service.CreateInvitation(inviter, ["a:1"], TimeSpan.FromMinutes(5));
        PairingResponse valid = _service.CreateProof(joiner, "J", token);

        // Подменяем заявленный Device ID на чужой — не совпадёт с хешем ключа.
        var tampered = valid with { DeviceId = other.Id.ToString() };

        Assert.Equal(PairingOutcome.IdentityMismatch, _service.VerifyProof(token, tampered).Outcome);
    }

    [Fact]
    public void Signature_by_foreign_key_is_rejected()
    {
        using var inviter = DeviceIdentity.Generate();
        using var joiner = DeviceIdentity.Generate();
        using var attacker = DeviceIdentity.Generate();
        PairingToken token = _service.CreateInvitation(inviter, ["a:1"], TimeSpan.FromMinutes(5));

        // Подпись делает attacker, но выдаёт ключ/ID joiner'а: id совпадёт с ключом
        // joiner'а, а подпись — нет.
        byte[] message = PairingMessage.Build(token.Secret, token.InviterDeviceId, joiner.Id, joiner.ExportPublicKey());
        var response = new PairingResponse(joiner.Id.ToString(), joiner.ExportPublicKey(), "J", attacker.Sign(message));

        Assert.Equal(PairingOutcome.BadSignature, _service.VerifyProof(token, response).Outcome);
    }

    [Fact]
    public void Joiner_trusts_inviter_from_token()
    {
        using var inviter = DeviceIdentity.Generate();
        PairingToken token = _service.CreateInvitation(inviter, ["a:1"], TimeSpan.FromMinutes(5));

        var trust = _service.CreateInviterTrust(token, "My Laptop");

        Assert.Equal(inviter.Id, trust.Id);
        Assert.Equal(inviter.ExportPublicKey(), trust.PublicKey);
        Assert.Equal("My Laptop", trust.Name);
    }
}
