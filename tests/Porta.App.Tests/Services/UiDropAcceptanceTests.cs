using System;
using System.Threading.Tasks;
using Porta.App.Services;
using Porta.Core.Drop;
using Porta.Core.Identity;
using Porta.Core.Protocol;

namespace Porta.App.Tests.Services;

public class UiDropAcceptanceTests
{
    private static readonly DeviceId Sender = DeviceIdentity.Generate().Id;

    private static DropOfferMessage Offer()
        => new("t1", [new DropFileInfo("photo.jpg", 1024, 0)]);

    private static UiDropAcceptance Create(string folder = "/downloads", TimeSpan? timeout = null)
        => new(() => folder, timeout ?? TimeSpan.FromSeconds(5));

    [Fact]
    public async Task Offer_reaches_the_ui_and_accepting_returns_the_folder()
    {
        UiDropAcceptance acceptance = Create("/downloads/porta");
        PendingDropOffer? shown = null;
        acceptance.OfferReceived += o => shown = o;

        Task<DropDecision> decision = acceptance.DecideAsync(Offer(), Sender);
        Assert.NotNull(shown);
        shown!.Accept();

        DropDecision result = await decision;
        Assert.True(result.Accepted);
        Assert.Equal("/downloads/porta", result.DestinationFolder);
    }

    [Fact]
    public async Task Rejecting_returns_the_reason_and_no_folder()
    {
        UiDropAcceptance acceptance = Create();
        PendingDropOffer? shown = null;
        acceptance.OfferReceived += o => shown = o;

        Task<DropDecision> decision = acceptance.DecideAsync(Offer(), Sender);
        shown!.Reject("Не сейчас");

        DropDecision result = await decision;
        Assert.False(result.Accepted);
        Assert.Equal("Не сейчас", result.Reason);
        Assert.Null(result.DestinationFolder);
    }

    [Fact]
    public async Task Nobody_at_the_screen_means_refusal_not_an_endless_wait()
    {
        UiDropAcceptance acceptance = Create(timeout: TimeSpan.FromMilliseconds(150));

        DropDecision result = await acceptance.DecideAsync(Offer(), Sender);

        Assert.False(result.Accepted);
        Assert.Equal("Получатель не ответил", result.Reason);
    }

    [Fact]
    public async Task Offer_is_closed_after_a_decision()
    {
        UiDropAcceptance acceptance = Create();
        PendingDropOffer? shown = null;
        PendingDropOffer? closed = null;
        acceptance.OfferReceived += o => shown = o;
        acceptance.OfferClosed += o => closed = o;

        Task<DropDecision> decision = acceptance.DecideAsync(Offer(), Sender);
        shown!.Accept();
        await decision;

        Assert.Same(shown, closed);
    }

    [Fact]
    public async Task Second_decision_on_the_same_offer_is_ignored()
    {
        UiDropAcceptance acceptance = Create();
        PendingDropOffer? shown = null;
        acceptance.OfferReceived += o => shown = o;

        Task<DropDecision> decision = acceptance.DecideAsync(Offer(), Sender);
        shown!.Accept();
        shown.Reject("поздно передумал");

        DropDecision result = await decision;
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task Folder_is_read_at_decision_time_not_at_offer_time()
    {
        string folder = "/старая";
        var acceptance = new UiDropAcceptance(() => folder, TimeSpan.FromSeconds(5));
        PendingDropOffer? shown = null;
        acceptance.OfferReceived += o => shown = o;

        Task<DropDecision> decision = acceptance.DecideAsync(Offer(), Sender);
        folder = "/новая";
        shown!.Accept();

        Assert.Equal("/новая", (await decision).DestinationFolder);
    }

    [Fact]
    public void Progress_reports_reach_the_ui()
    {
        UiDropAcceptance acceptance = Create();
        Porta.Core.Sync.TransferProgress? seen = null;
        acceptance.ProgressChanged += p => seen = p;

        acceptance.Progress.Report(new Porta.Core.Sync.TransferProgress(1, 3, 100, 300, "a.jpg"));

        Assert.NotNull(seen);
        Assert.Equal(100, seen!.BytesDone);
        Assert.Equal("a.jpg", seen.CurrentFile);
    }

    [Fact]
    public async Task Sender_and_contents_are_visible_to_the_ui()
    {
        UiDropAcceptance acceptance = Create();
        PendingDropOffer? shown = null;
        acceptance.OfferReceived += o => shown = o;

        Task<DropDecision> decision = acceptance.DecideAsync(
            new DropOfferMessage("t2", [new DropFileInfo("a.jpg", 2048, 0), new DropFileInfo("b.mp4", 4096, 0)]),
            Sender);
        shown!.Reject();
        await decision;

        Assert.Equal(2, shown.FileCount);
        Assert.Equal(6144, shown.TotalSize);
        Assert.Equal(Sender, shown.Sender);
    }
}
