using System;
using Porta.App.Design;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.Tests;

/// <summary>Управляемый фейк данных приложения для тестов view-моделей.</summary>
internal sealed class FakeAppData : IAppData
{
    public string DeviceName { get; set; } = "Test Device";
    public string DeviceId { get; set; } = "TEST-ID";
    public IStorageRepository Storages { get; } = new InMemoryStorageRepository();
    public IDeviceRepository Devices { get; } = new InMemoryDeviceRepository();

    public string InvitationTokenValue { get; set; } = "porta:fake-token";

    public string CreateInvitation() => InvitationTokenValue;

    public TrustedDevice AcceptInvitation(string token, string deviceName)
    {
        if (token == "bad")
            throw new FormatException("bad token");

        using var identity = DeviceIdentity.Generate();
        var device = new TrustedDevice(
            identity.Id,
            identity.ExportPublicKey(),
            string.IsNullOrWhiteSpace(deviceName) ? "Устройство" : deviceName.Trim(),
            DateTimeOffset.UnixEpoch,
            null);
        Devices.Add(device);
        return device;
    }
}
