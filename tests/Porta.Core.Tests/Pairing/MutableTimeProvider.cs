namespace Porta.Core.Tests.Pairing;

/// <summary>Простой управляемый источник времени для тестов.</summary>
internal sealed class MutableTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
