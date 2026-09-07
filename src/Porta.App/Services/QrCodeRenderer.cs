using System;
using QRCoder;

namespace Porta.App.Services;

/// <summary>
/// Превращает строку (токен связывания) в QR-код. За интерфейсом, чтобы зависимость от
/// библиотеки жила в одном месте, а view-модель тестировалась без неё.
/// См. docs/features/30-qr-pairing.md.
/// </summary>
public interface IQrCodeRenderer
{
    /// <summary>Отрисовать QR в PNG. Бросает <see cref="System.ArgumentException"/> на пустой ввод.</summary>
    byte[] RenderPng(string content, int pixelsPerModule = 8);
}

/// <summary>
/// Реализация на QRCoder. Используется только <c>PngByteQRCode</c> — чистый managed-код,
/// типы <c>System.Drawing</c> не затрагиваются (см. оговорку в docs/features/30-qr-pairing.md).
/// </summary>
public sealed class QrCoderRenderer : IQrCodeRenderer
{
    public byte[] RenderPng(string content, int pixelsPerModule = 8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelsPerModule, 1);

        // Коррекция Q: токен читается с экрана, где возможны блики и муар.
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }
}
