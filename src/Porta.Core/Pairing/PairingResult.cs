using Porta.Core.Model;

namespace Porta.Core.Pairing;

/// <summary>Исход проверки связывания.</summary>
public enum PairingOutcome
{
    Success,
    Expired,
    UnsupportedVersion,
    IdentityMismatch,
    BadSignature,
}

/// <summary>
/// Результат проверки доказательства связывания. При успехе содержит доверенное
/// устройство для сохранения.
/// </summary>
public sealed record PairingResult(PairingOutcome Outcome, TrustedDevice? Device)
{
    public bool IsSuccess => Outcome == PairingOutcome.Success;

    public static PairingResult Success(TrustedDevice device) => new(PairingOutcome.Success, device);

    public static PairingResult Fail(PairingOutcome outcome) => new(outcome, null);
}
