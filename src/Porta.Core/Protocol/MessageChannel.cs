using System.Buffers.Binary;
using MessagePack;

namespace Porta.Core.Protocol;

/// <summary>
/// Фрейминг сообщений поверх потока: 4 байта длины (BE) + MessagePack-полезная нагрузка.
/// Данные приходят от второй стороны → десериализация в режиме недоверенных данных.
/// См. docs/features/07-protocol.md.
/// </summary>
public sealed class MessageChannel
{
    /// <summary>Максимальный размер одного сообщения (защита от «бомбы» длины).</summary>
    public const int MaxMessageSize = 16 * 1024 * 1024;

    /// <summary>
    /// Сколько ждать одного сообщения по умолчанию. Ограничивает **одно** чтение, а не
    /// весь диалог: длинная передача идёт пачками и в лимит не упирается, а мёртвый пир
    /// больше не вешает нашу сторону навсегда. См. docs/features/27-block-streaming.md.
    /// </summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(2);

    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    private readonly Stream _stream;

    public MessageChannel(Stream stream) => _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// Предел ожидания одного сообщения. <see cref="Timeout.InfiniteTimeSpan"/> — ждать
    /// без ограничения (осознанный выбор вызывающего, не значение по умолчанию).
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = DefaultIdleTimeout;

    public async ValueTask WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        byte[] payload = MessagePackSerializer.Serialize(message, Options, cancellationToken);
        if (payload.Length > MaxMessageSize)
            throw new InvalidOperationException($"Сообщение больше лимита {MaxMessageSize} байт.");

        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<T> ReadAsync<T>(CancellationToken cancellationToken = default)
    {
        using var idle = CreateIdleScope(cancellationToken);
        CancellationToken token = idle?.Token ?? cancellationToken;

        try
        {
            byte[] header = new byte[4];
            await _stream.ReadExactlyAsync(header, token).ConfigureAwait(false);

            int length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (length < 0 || length > MaxMessageSize)
                throw new InvalidDataException($"Недопустимая длина сообщения: {length}.");

            byte[] payload = new byte[length];
            await _stream.ReadExactlyAsync(payload, token).ConfigureAwait(false);
            return MessagePackSerializer.Deserialize<T>(payload, Options, token);
        }
        catch (OperationCanceledException) when (idle is not null && idle.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Вторая сторона молчит дольше {IdleTimeout}.");
        }
    }

    /// <summary>Токен, который сам сработает по <see cref="IdleTimeout"/>; null — без предела.</summary>
    private CancellationTokenSource? CreateIdleScope(CancellationToken cancellationToken)
    {
        if (IdleTimeout == Timeout.InfiniteTimeSpan)
            return null;

        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(IdleTimeout);
        return source;
    }
}
