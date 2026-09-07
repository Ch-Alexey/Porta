using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Porta.App.Converters;

/// <summary>
/// Байты PNG → картинка для <c>Image.Source</c>. Живёт во view-слое, чтобы view-модели
/// отдавали данные, а не графические объекты. См. docs/features/30-qr-pairing.md.
/// </summary>
public sealed class PngToBitmapConverter : IValueConverter
{
    public static PngToBitmapConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } png)
            return null;

        try
        {
            using var stream = new MemoryStream(png);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            // Битые байты — лучше пустое место, чем падение окна.
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Обратное преобразование картинки не нужно.");
}
