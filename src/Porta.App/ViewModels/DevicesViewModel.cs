using System.Collections.ObjectModel;
using Porta.Core.App;
using Porta.Core.Model;

namespace Porta.App.ViewModels;

/// <summary>Вкладка «Устройства»: список доверенных устройств.</summary>
public sealed class DevicesViewModel : ViewModelBase
{
    public DevicesViewModel(AppEnvironment environment)
    {
        foreach (TrustedDevice device in environment.Devices.List())
            Items.Add(new DeviceItem(device.Name, device.Id.ToDisplayString()));
    }

    public ObservableCollection<DeviceItem> Items { get; } = [];
}

/// <summary>Строка списка устройств для отображения.</summary>
public sealed record DeviceItem(string Name, string DeviceId);
