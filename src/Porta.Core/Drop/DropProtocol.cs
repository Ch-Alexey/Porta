using System.Security.Cryptography;
using Porta.Core.Identity;
using Porta.Core.Protocol;
using Porta.Core.Sync;

namespace Porta.Core.Drop;

/// <summary>
/// Разовая передача файлов поверх канала сообщений: отправитель предлагает, получатель
/// подтверждает, содержимое идёт потоком. См. docs/features/26-drop-transfer.md.
/// </summary>
public static class DropProtocol
{
    /// <summary>Размер куска содержимого — память не зависит от размера файла.</summary>
    public const int ChunkSize = 256 * 1024;

    /// <summary>
    /// Отправляющая сторона: предложить файлы и, если приняли, передать содержимое.
    /// </summary>
    /// <param name="channel">Канал установленной сессии.</param>
    /// <param name="files">Что отправляем: полный путь → путь внутри передачи.</param>
    /// <param name="transferId">Идентификатор передачи (для UI получателя).</param>
    /// <param name="cancellationToken">Отмена.</param>
    public static async Task<DropSendResult> SendAsync(
        MessageChannel channel,
        IReadOnlyList<DropSourceFile> files,
        string? transferId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(files);

        var offer = new DropOfferMessage(
            transferId ?? Guid.NewGuid().ToString("N"),
            files.Select(Describe).ToList());

        await channel.WriteAsync(offer, cancellationToken).ConfigureAwait(false);
        DropDecisionMessage decision = await channel.ReadAsync<DropDecisionMessage>(cancellationToken).ConfigureAwait(false);

        if (!decision.Accepted)
        {
            // Подтверждаем отказ: пока приёмник ждёт этого сообщения, он не закроет
            // соединение — иначе оно рвётся прямо под нашим чтением отказа выше.
            await SayGoodbyeAsync(channel, cancellationToken).ConfigureAwait(false);
            return new DropSendResult(false, 0, 0, decision.Reason ?? "Получатель отклонил передачу");
        }

        long bytes = 0;
        foreach (DropSourceFile file in files)
            bytes += await SendFileAsync(channel, file, cancellationToken).ConfigureAwait(false);

        // Ждём подтверждения: получатель дописал всё на диск, соединение можно закрывать.
        // Best-effort — данные уже переданы, обрыв на этом шаге результата не меняет.
        try
        {
            _ = await channel.ReadAsync<DropCompleteMessage>(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Получатель закрыл соединение после приёма — это норма.
        }

        return new DropSendResult(true, files.Count, bytes);
    }

    /// <summary>
    /// Принимающая сторона: спросить у <paramref name="acceptance"/> разрешение и, если
    /// дали, записать файлы в указанную папку.
    /// </summary>
    public static async Task<DropReceiveResult> ReceiveAsync(
        MessageChannel channel,
        IDropAcceptance acceptance,
        DeviceId sender,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(acceptance);

        DropOfferMessage offer = await channel.ReadAsync<DropOfferMessage>(cancellationToken).ConfigureAwait(false);
        DropDecision decision = await acceptance.DecideAsync(offer, sender, cancellationToken).ConfigureAwait(false);

        if (!decision.Accepted || string.IsNullOrWhiteSpace(decision.DestinationFolder))
        {
            await channel.WriteAsync(
                new DropDecisionMessage(false, decision.Reason ?? "Получатель отклонил передачу"),
                cancellationToken).ConfigureAwait(false);

            // Не закрываем соединение сразу: отправитель ещё читает отказ, а обрыв QUIC
            // выбрасывает недочитанное и превращает вежливый отказ в исключение.
            await WaitForGoodbyeAsync(channel, cancellationToken).ConfigureAwait(false);
            return DropReceiveResult.Rejected;
        }

        string destination = decision.DestinationFolder;
        Directory.CreateDirectory(destination);
        await channel.WriteAsync(new DropDecisionMessage(true), cancellationToken).ConfigureAwait(false);

        var written = new List<string>(offer.Files.Count);
        long bytes = 0;
        foreach (DropFileInfo _ in offer.Files)
        {
            (string path, long size) = await ReceiveFileAsync(channel, destination, cancellationToken).ConfigureAwait(false);
            written.Add(path);
            bytes += size;
        }

        // Подтверждаем приём — сигнал отправителю, что можно закрывать соединение.
        try
        {
            await channel.WriteAsync(new DropCompleteMessage(written.Count), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Отправитель уже закрыл соединение — файлы всё равно записаны.
        }

        return new DropReceiveResult(true, written.Count, bytes, written);
    }

    /// <summary>
    /// Сколько ждать прощального сообщения. Короче общего таймаута канала: данных здесь
    /// уже нет, и висеть минутами из-за упавшего пира незачем.
    /// </summary>
    private static readonly TimeSpan GoodbyeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Сказать «отказ понял» — best-effort, на результат передачи не влияет.</summary>
    private static async Task SayGoodbyeAsync(MessageChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            await channel.WriteAsync(new DropCompleteMessage(0), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Приёмник уже ушёл — отказ мы всё равно получили.
        }
    }

    /// <summary>Дождаться подтверждения отказа — best-effort, с коротким пределом.</summary>
    private static async Task WaitForGoodbyeAsync(MessageChannel channel, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(GoodbyeTimeout);
        try
        {
            _ = await channel.ReadAsync<DropCompleteMessage>(cts.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or TimeoutException)
        {
            // Отправитель не попрощался — отказ уже отправлен, больше делать нечего.
        }
    }

    private static async Task<long> SendFileAsync(
        MessageChannel channel,
        DropSourceFile file,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(file.FullPath);
        byte[] contentHash;
        using (FileStream hashStream = File.OpenRead(file.FullPath))
            contentHash = await SHA256.HashDataAsync(hashStream, cancellationToken).ConfigureAwait(false);

        await channel.WriteAsync(
            new DropFileHeaderMessage(file.RelativePath, info.Length, ToUnixMs(info.LastWriteTimeUtc), contentHash),
            cancellationToken).ConfigureAwait(false);

        long sent = 0;
        byte[] buffer = new byte[ChunkSize];
        using FileStream stream = File.OpenRead(file.FullPath);
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                // Явный признак конца: пустой последний кусок корректен для пустого файла.
                await channel.WriteAsync(new DropChunkMessage([], true), cancellationToken).ConfigureAwait(false);
                break;
            }

            sent += read;
            await channel.WriteAsync(new DropChunkMessage(buffer[..read], false), cancellationToken).ConfigureAwait(false);
        }

        return sent;
    }

    private static async Task<(string Path, long Size)> ReceiveFileAsync(
        MessageChannel channel,
        string destination,
        CancellationToken cancellationToken)
    {
        DropFileHeaderMessage header =
            await channel.ReadAsync<DropFileHeaderMessage>(cancellationToken).ConfigureAwait(false);

        // Путь пришёл из сети — проверяем до любой записи на диск.
        string requested = SafePath.Resolve(destination, header.RelativePath);
        string tempPath = Path.Combine(
            Path.GetDirectoryName(requested)!,
            "." + Path.GetFileName(requested) + ".porta-drop");
        Directory.CreateDirectory(Path.GetDirectoryName(requested)!);

        long size = 0;
        try
        {
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                using (FileStream stream = File.Create(tempPath))
                {
                    while (true)
                    {
                        DropChunkMessage chunk =
                            await channel.ReadAsync<DropChunkMessage>(cancellationToken).ConfigureAwait(false);

                        if (chunk.Data.Length > 0)
                        {
                            size += chunk.Data.Length;
                            hasher.AppendData(chunk.Data);
                            await stream.WriteAsync(chunk.Data, cancellationToken).ConfigureAwait(false);
                        }

                        if (chunk.IsLast)
                            break;
                    }
                }

                if (!hasher.GetHashAndReset().AsSpan().SequenceEqual(header.ContentHash))
                    throw new InvalidDataException($"Контент-хеш принятого файла не совпал: {header.RelativePath}.");
            }

            // Такой же файл уже лежит — второй экземпляр не нужен. Проверяем именно
            // содержимое, а не имя: цель «файл есть у получателя» уже достигнута.
            // См. docs/features/31-audit-fixes.md.
            if (SameContentOnDisk(requested, header.ContentHash))
            {
                File.Delete(tempPath);
                return (requested, size);
            }

            // Разное содержимое под одним именем — чужие данные не затираем, кладём рядом.
            string finalPath = FreePath(requested);
            File.Move(tempPath, finalPath);
            File.SetLastWriteTimeUtc(finalPath, FromUnixMs(header.ModifiedAtUnixMs));
            return (finalPath, size);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    /// <summary>Лежит ли по этому пути файл ровно с таким содержимым.</summary>
    private static bool SameContentOnDisk(string path, byte[] contentHash)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(path);
            return SHA256.HashData(stream).AsSpan().SequenceEqual(contentHash);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Не смогли прочитать — считаем, что файл другой, и кладём копию рядом.
            return false;
        }
    }

    /// <summary>Свободное имя рядом с занятым: «фото.jpg» → «фото (2).jpg».</summary>
    private static string FreePath(string path)
    {
        if (!File.Exists(path))
            return path;

        string directory = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static DropFileInfo Describe(DropSourceFile file)
    {
        var info = new FileInfo(file.FullPath);
        if (!info.Exists)
            throw new FileNotFoundException($"Файл для передачи не найден: {file.FullPath}", file.FullPath);
        return new DropFileInfo(file.RelativePath, info.Length, ToUnixMs(info.LastWriteTimeUtc));
    }

    private static long ToUnixMs(DateTime utc)
        => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static DateTime FromUnixMs(long unixMs)
        => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
}

/// <summary>Файл, выбранный к отправке.</summary>
/// <param name="FullPath">Путь на диске отправителя.</param>
/// <param name="RelativePath">Как файл будет назван у получателя ('/' как разделитель).</param>
public sealed record DropSourceFile(string FullPath, string RelativePath)
{
    /// <summary>Одиночный файл — у получателя ляжет под своим именем.</summary>
    public static DropSourceFile FromPath(string fullPath)
        => new(fullPath, Path.GetFileName(fullPath));
}
