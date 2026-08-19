using System;
using Porta.App.Design;
using Porta.App.ViewModels;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.Tests.ViewModels;

public class StoragesViewModelTests
{
    private static StoragesViewModel NewVm(
        InMemoryStorageRepository? storages = null,
        InMemoryDeviceRepository? devices = null)
        => new(storages ?? new InMemoryStorageRepository(), devices ?? new InMemoryDeviceRepository());

    [Fact]
    public void Starts_empty_when_repository_is_empty()
    {
        Assert.Empty(NewVm().Items);
    }

    [Fact]
    public void Add_persists_storage_and_shows_it()
    {
        var repo = new InMemoryStorageRepository();
        var vm = NewVm(repo);
        vm.NewName = "Docs";
        vm.NewPath = "/home/me/docs";

        vm.AddCommand.Execute(null);

        StorageItem item = Assert.Single(vm.Items);
        Assert.Equal("Docs", item.Name);
        Assert.Equal("/home/me/docs", item.LocalPath);
        Assert.Single(repo.List());
        Assert.Equal(string.Empty, vm.NewName);
        Assert.Equal(string.Empty, vm.NewPath);
    }

    [Theory]
    [InlineData("", "/path")]
    [InlineData("Name", "")]
    [InlineData("   ", "   ")]
    public void Add_ignores_blank_input(string name, string path)
    {
        var vm = NewVm();
        vm.NewName = name;
        vm.NewPath = path;

        vm.AddCommand.Execute(null);

        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Trusted_devices_are_listed_for_sharing()
    {
        var devices = new InMemoryDeviceRepository();
        using var d = DeviceIdentity.Generate();
        devices.Add(new TrustedDevice(d.Id, d.ExportPublicKey(), "Phone", DateTimeOffset.UnixEpoch, null));

        var vm = NewVm(devices: devices);

        Assert.Equal("Phone", Assert.Single(vm.TrustedDevices).Name);
    }

    [Fact]
    public void Share_links_storage_to_device_and_shows_it()
    {
        var storages = new InMemoryStorageRepository();
        var devices = new InMemoryDeviceRepository();
        using var d = DeviceIdentity.Generate();
        devices.Add(new TrustedDevice(d.Id, d.ExportPublicKey(), "Phone", DateTimeOffset.UnixEpoch, null));
        var vm = NewVm(storages, devices);
        vm.NewName = "Docs";
        vm.NewPath = "/docs";
        vm.AddCommand.Execute(null);

        vm.SelectedStorage = vm.Items[0];
        vm.SelectedDevice = vm.TrustedDevices[0];
        vm.ShareCommand.Execute(null);

        Assert.Contains("Phone", vm.Items[0].SharedWith);
        Assert.Single(storages.ListDevices(vm.Items[0].Id));
    }

    [Fact]
    public void Unshare_removes_the_link()
    {
        var storages = new InMemoryStorageRepository();
        var devices = new InMemoryDeviceRepository();
        using var d = DeviceIdentity.Generate();
        devices.Add(new TrustedDevice(d.Id, d.ExportPublicKey(), "Phone", DateTimeOffset.UnixEpoch, null));
        var vm = NewVm(storages, devices);
        vm.NewName = "Docs";
        vm.NewPath = "/docs";
        vm.AddCommand.Execute(null);
        vm.SelectedStorage = vm.Items[0];
        vm.SelectedDevice = vm.TrustedDevices[0];
        vm.ShareCommand.Execute(null);

        vm.SelectedStorage = vm.Items[0];
        vm.SelectedDevice = vm.TrustedDevices[0];
        vm.UnshareCommand.Execute(null);

        Assert.Empty(storages.ListDevices(vm.Items[0].Id));
    }
}
