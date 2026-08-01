using System;
using Porta.App.ViewModels;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Exposes_device_identity_and_child_tabs()
    {
        var vm = new MainViewModel(new FakeAppData());

        Assert.Equal("Test Device", vm.DeviceName);
        Assert.Equal("TEST-ID", vm.DeviceId);
        Assert.NotNull(vm.Storages);
        Assert.NotNull(vm.Devices);
    }

    [Fact]
    public void Devices_tab_lists_trusted_devices()
    {
        var data = new FakeAppData();
        using var identity = DeviceIdentity.Generate();
        data.Devices.Add(new TrustedDevice(identity.Id, identity.ExportPublicKey(), "Phone",
            DateTimeOffset.UnixEpoch, null));

        var vm = new MainViewModel(data);

        DeviceItem item = Assert.Single(vm.Devices.Items);
        Assert.Equal("Phone", item.Name);
    }
}
