using System.Security.Cryptography;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Pairing;

/// <summary>
/// Логика связывания устройств: создание приглашения, доказательство joiner'а,
/// проверка на инициаторе, установление доверия. Сетевого обмена здесь нет —
/// только чистая логика. См. docs/features/04-pairing.md.
/// </summary>
public sealed class PairingService
{
    /// <summary>Длина одноразового кода (байты).</summary>
    public const int SecretSize = 32;

    private readonly TimeProvider _clock;

    public PairingService(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// Инициатор: создать токен приглашения со свежим одноразовым кодом.
    /// </summary>
    public PairingToken CreateInvitation(DeviceIdentity self, IReadOnlyList<string> addresses, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(addresses);

        byte[] secret = RandomNumberGenerator.GetBytes(SecretSize);
        long expiresAt = _clock.GetUtcNow().Add(lifetime).ToUnixTimeSeconds();

        return new PairingToken(
            PairingToken.CurrentVersion,
            self.ExportPublicKey(),
            addresses.ToArray(),
            secret,
            expiresAt);
    }

    /// <summary>
    /// joiner: создать доказательство владения кодом, связанное со своей личностью.
    /// </summary>
    public PairingResponse CreateProof(DeviceIdentity self, string selfName, PairingToken token)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(token);

        byte[] message = PairingMessage.Build(
            token.Secret,
            token.InviterDeviceId,
            self.Id,
            self.ExportPublicKey());

        return new PairingResponse(
            self.Id.ToString(),
            self.ExportPublicKey(),
            selfName,
            self.Sign(message));
    }

    /// <summary>
    /// Инициатор: проверить доказательство joiner'а. При успехе вернуть доверенное
    /// устройство для сохранения.
    /// </summary>
    public PairingResult VerifyProof(PairingToken token, PairingResponse response)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(response);

        if (token.Version != PairingToken.CurrentVersion)
            return PairingResult.Fail(PairingOutcome.UnsupportedVersion);

        if (_clock.GetUtcNow().ToUnixTimeSeconds() > token.ExpiresAtUnix)
            return PairingResult.Fail(PairingOutcome.Expired);

        if (!DeviceId.TryParse(response.DeviceId, out DeviceId? claimedId))
            return PairingResult.Fail(PairingOutcome.IdentityMismatch);

        DeviceId computedId = DeviceId.FromPublicKey(response.PublicKey);
        if (claimedId != computedId)
            return PairingResult.Fail(PairingOutcome.IdentityMismatch);

        byte[] message = PairingMessage.Build(
            token.Secret,
            token.InviterDeviceId,
            claimedId!,
            response.PublicKey);

        if (!DeviceIdentity.VerifyWithPublicKey(response.PublicKey, message, response.Signature))
            return PairingResult.Fail(PairingOutcome.BadSignature);

        var trusted = new TrustedDevice(
            claimedId!,
            response.PublicKey,
            response.Name,
            _clock.GetUtcNow(),
            LastSeenAt: null);

        return PairingResult.Success(trusted);
    }

    /// <summary>
    /// joiner: установить доверие к инициатору по токену (ключ берётся прямо из QR).
    /// </summary>
    public TrustedDevice CreateInviterTrust(PairingToken token, string inviterName)
    {
        ArgumentNullException.ThrowIfNull(token);

        return new TrustedDevice(
            token.InviterDeviceId,
            token.InviterPublicKey,
            inviterName,
            _clock.GetUtcNow(),
            LastSeenAt: null);
    }
}
