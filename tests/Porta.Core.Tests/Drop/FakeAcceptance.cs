using Porta.Core.Drop;
using Porta.Core.Identity;
using Porta.Core.Protocol;

namespace Porta.Core.Tests.Drop;

/// <summary>Подтверждение приёма с заранее заданным решением; запоминает предложение.</summary>
internal sealed class FakeAcceptance(DropDecision decision) : IDropAcceptance
{
    public DropOfferMessage? SeenOffer { get; private set; }

    public DeviceId? SeenSender { get; private set; }

    public Task<DropDecision> DecideAsync(
        DropOfferMessage offer,
        DeviceId sender,
        CancellationToken cancellationToken = default)
    {
        SeenOffer = offer;
        SeenSender = sender;
        return Task.FromResult(decision);
    }
}
