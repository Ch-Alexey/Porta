using Porta.App.ViewModels;

namespace Porta.App.Tests.ViewModels;

public class DevicesViewModelTests
{
    [Fact]
    public void GenerateInvitation_shows_token()
    {
        var data = new FakeAppData { InvitationTokenValue = "porta:abc123" };
        var vm = new DevicesViewModel(data);

        vm.GenerateInvitationCommand.Execute(null);

        Assert.Equal("porta:abc123", vm.InvitationToken);
    }

    [Fact]
    public void Connect_with_valid_token_adds_device_and_clears_input()
    {
        var vm = new DevicesViewModel(new FakeAppData())
        {
            PastedToken = "porta:valid",
            NewDeviceName = "Phone",
        };

        vm.ConnectCommand.Execute(null);

        DeviceItem item = Assert.Single(vm.Items);
        Assert.Equal("Phone", item.Name);
        Assert.Equal(string.Empty, vm.PastedToken);
        Assert.NotNull(vm.StatusMessage);
    }

    [Fact]
    public void Connect_with_blank_token_does_nothing()
    {
        var vm = new DevicesViewModel(new FakeAppData()) { PastedToken = "   " };

        vm.ConnectCommand.Execute(null);

        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Connect_with_invalid_token_reports_error_and_adds_nothing()
    {
        var vm = new DevicesViewModel(new FakeAppData()) { PastedToken = "bad" };

        vm.ConnectCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.Equal("Некорректный токен связывания", vm.StatusMessage);
    }
}
