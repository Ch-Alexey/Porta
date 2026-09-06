using System;
using System.Threading;
using System.Threading.Tasks;
using Porta.Core.Drop;
using Porta.Core.Identity;
using Porta.Core.Protocol;

namespace Porta.App.Services;

/// <summary>
/// Мост между ядром и человеком: ядро спрашивает «принимать?», вопрос всплывает в UI,
/// ответ возвращается ядру. См. docs/features/28-drop-ui.md.
/// </summary>
public sealed class UiDropAcceptance : IDropAcceptance
{
    /// <summary>
    /// Сколько ждать человека. Если у экрана никого нет, отправитель не должен висеть —
    /// молчание считается отказом.
    /// </summary>
    public static readonly TimeSpan DefaultDecisionTimeout = TimeSpan.FromMinutes(2);

    private readonly Func<string> _destinationFolder;
    private readonly TimeSpan _timeout;

    /// <param name="destinationFolder">Куда складывать принятое (читается на момент вопроса).</param>
    /// <param name="timeout">Сколько ждать ответа человека.</param>
    public UiDropAcceptance(Func<string> destinationFolder, TimeSpan? timeout = null)
    {
        _destinationFolder = destinationFolder;
        _timeout = timeout ?? DefaultDecisionTimeout;
    }

    /// <summary>Появилось входящее предложение — UI должен его показать.</summary>
    public event Action<PendingDropOffer>? OfferReceived;

    /// <summary>Предложение закрыто (решено или протухло) — UI должен его убрать.</summary>
    public event Action<PendingDropOffer>? OfferClosed;

    public async Task<DropDecision> DecideAsync(
        DropOfferMessage offer,
        DeviceId sender,
        CancellationToken cancellationToken = default)
    {
        var pending = new PendingDropOffer(offer, sender);
        OfferReceived?.Invoke(pending);

        try
        {
            return await pending.WaitAsync(_timeout, _destinationFolder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            OfferClosed?.Invoke(pending);
        }
    }
}

/// <summary>
/// Входящее предложение, ожидающее решения человека. Решение принимается один раз:
/// повторные нажатия игнорируются.
/// </summary>
public sealed class PendingDropOffer
{
    private readonly TaskCompletionSource<DropDecision> _answer =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal PendingDropOffer(DropOfferMessage offer, DeviceId sender)
    {
        Offer = offer;
        Sender = sender;
    }

    /// <summary>Что предлагают.</summary>
    public DropOfferMessage Offer { get; }

    /// <summary>Кто предлагает (Device ID подтверждён транспортом).</summary>
    public DeviceId Sender { get; }

    /// <summary>Число файлов в предложении.</summary>
    public int FileCount => Offer.Files.Count;

    /// <summary>Суммарный объём предложения.</summary>
    public long TotalSize => Offer.TotalSize;

    /// <summary>Принять передачу. Второй вызов ничего не делает.</summary>
    public void Accept() => _answer.TrySetResult(new DropDecision(true));

    /// <summary>Отклонить передачу. Второй вызов ничего не делает.</summary>
    public void Reject(string reason = "Получатель отклонил передачу")
        => _answer.TrySetResult(DropDecision.Reject(reason));

    /// <summary>
    /// Дождаться решения. Молчание дольше <paramref name="timeout"/> — отказ:
    /// отправитель узнаёт причину вместо бесконечного ожидания.
    /// </summary>
    internal async Task<DropDecision> WaitAsync(
        TimeSpan timeout,
        Func<string> destinationFolder,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        using CancellationTokenRegistration registration = cts.Token.Register(
            () => _answer.TrySetResult(DropDecision.Reject("Получатель не ответил")));

        DropDecision decision = await _answer.Task.ConfigureAwait(false);

        // Папку подставляем здесь, а не в Accept(): к моменту ответа настройка могла
        // измениться, и брать надо актуальную.
        return decision.Accepted ? DropDecision.Accept(destinationFolder()) : decision;
    }
}
