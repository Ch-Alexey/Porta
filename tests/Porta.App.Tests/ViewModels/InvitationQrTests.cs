using System;
using Porta.App.Services;
using Porta.App.ViewModels;

namespace Porta.App.Tests.ViewModels;

/// <summary>Приглашение показывается картинкой. См. docs/features/30-qr-pairing.md.</summary>
public class InvitationQrTests
{
    /// <summary>Рендер, который записывает, что ему дали, и отдаёт узнаваемые байты.</summary>
    private sealed class FakeQr : IQrCodeRenderer
    {
        public string? Rendered { get; private set; }
        public Exception? Throws { get; set; }

        public byte[] RenderPng(string content, int pixelsPerModule = 8)
        {
            if (Throws is not null)
                throw Throws;
            Rendered = content;
            return [1, 2, 3, 4];
        }
    }

    private static DevicesViewModel Create(IQrCodeRenderer qr)
        => new(new FakeAppData(), dispatcher: new ImmediateDispatcher(), qr: qr);

    [Fact]
    public void Generating_an_invitation_renders_the_same_token_as_the_text()
    {
        var qr = new FakeQr();
        DevicesViewModel vm = Create(qr);

        vm.GenerateInvitationCommand.Execute(null);

        Assert.NotEmpty(vm.InvitationToken);
        Assert.Equal(vm.InvitationToken, qr.Rendered);
        Assert.Equal([1, 2, 3, 4], vm.InvitationQrPng);
    }

    [Fact]
    public void Nothing_is_shown_before_an_invitation_is_generated()
        => Assert.Null(Create(new FakeQr()).InvitationQrPng);

    [Fact]
    public void Hiding_the_invitation_clears_both_the_text_and_the_picture()
    {
        DevicesViewModel vm = Create(new FakeQr());
        vm.GenerateInvitationCommand.Execute(null);

        vm.HideInvitationCommand.Execute(null);

        Assert.Equal(string.Empty, vm.InvitationToken);
        Assert.Null(vm.InvitationQrPng);
    }

    [Fact]
    public void A_broken_renderer_still_leaves_the_token_copyable()
    {
        var qr = new FakeQr { Throws = new InvalidOperationException("рендер сломался") };
        DevicesViewModel vm = Create(qr);

        vm.GenerateInvitationCommand.Execute(null);

        Assert.NotEmpty(vm.InvitationToken);
        Assert.Null(vm.InvitationQrPng);
    }
}
