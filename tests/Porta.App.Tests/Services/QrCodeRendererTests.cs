using System;
using System.Linq;
using Porta.App.Services;

namespace Porta.App.Tests.Services;

public class QrCodeRendererTests
{
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private const string SampleToken =
        "porta:lQHEWzBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABKq7-пример-токена-связывания-достаточной-длины";

    [Fact]
    public void Renders_a_real_png()
    {
        byte[] png = new QrCoderRenderer().RenderPng(SampleToken);

        Assert.Equal(PngSignature, png.Take(PngSignature.Length));
        Assert.True(png.Length > 200, $"картинка подозрительно мала: {png.Length} байт");
    }

    [Fact]
    public void Same_token_renders_identically()
    {
        var renderer = new QrCoderRenderer();

        Assert.Equal(renderer.RenderPng(SampleToken), renderer.RenderPng(SampleToken));
    }

    [Fact]
    public void Different_tokens_render_differently()
    {
        var renderer = new QrCoderRenderer();

        Assert.NotEqual(renderer.RenderPng(SampleToken), renderer.RenderPng(SampleToken + "X"));
    }

    [Fact]
    public void Bigger_scale_gives_a_bigger_image()
    {
        var renderer = new QrCoderRenderer();

        Assert.True(renderer.RenderPng(SampleToken, 16).Length > renderer.RenderPng(SampleToken, 4).Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_content_is_refused(string content)
        => Assert.Throws<ArgumentException>(() => new QrCoderRenderer().RenderPng(content));

    [Fact]
    public void Zero_scale_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new QrCoderRenderer().RenderPng(SampleToken, 0));

    [Fact]
    public void Cyrillic_content_is_encoded_without_throwing()
    {
        byte[] png = new QrCoderRenderer().RenderPng("porta:проверка-кириллицы-в-токене");

        Assert.Equal(PngSignature, png.Take(PngSignature.Length));
    }
}
