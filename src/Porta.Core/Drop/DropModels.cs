using Porta.Core.Identity;
using Porta.Core.Protocol;

namespace Porta.Core.Drop;

/// <summary>
/// Решение получателя по предложенной передаче. См. docs/features/26-drop-transfer.md.
/// </summary>
/// <param name="Accepted">Принять ли файлы.</param>
/// <param name="DestinationFolder">Куда складывать (обязательно при <c>Accepted</c>).</param>
/// <param name="Reason">Причина отказа — уходит отправителю.</param>
public sealed record DropDecision(bool Accepted, string? DestinationFolder = null, string? Reason = null)
{
    public static DropDecision Accept(string destinationFolder) => new(true, destinationFolder);

    public static DropDecision Reject(string reason) => new(false, null, reason);
}

/// <summary>
/// Спрашивает, принимать ли разовую передачу. Реализуется в UI: ядро только задаёт
/// вопрос и не знает ни про диалоги, ни про настройки папки «Загрузки».
/// </summary>
public interface IDropAcceptance
{
    Task<DropDecision> DecideAsync(DropOfferMessage offer, DeviceId sender, CancellationToken cancellationToken = default);
}

/// <summary>Итог отправки.</summary>
/// <param name="Accepted">Принял ли получатель.</param>
/// <param name="FilesSent">Сколько файлов отправлено.</param>
/// <param name="BytesSent">Сколько байт содержимого отправлено.</param>
/// <param name="RejectReason">Причина отказа, если не принял.</param>
public sealed record DropSendResult(bool Accepted, int FilesSent, long BytesSent, string? RejectReason = null);

/// <summary>Итог приёма.</summary>
/// <param name="Accepted">Приняли ли передачу.</param>
/// <param name="FilesReceived">Сколько файлов записано.</param>
/// <param name="BytesReceived">Сколько байт записано.</param>
/// <param name="Paths">Полные пути записанных файлов.</param>
public sealed record DropReceiveResult(
    bool Accepted,
    int FilesReceived,
    long BytesReceived,
    IReadOnlyList<string> Paths)
{
    public static DropReceiveResult Rejected { get; } = new(false, 0, 0, []);
}
