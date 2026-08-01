using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Core.App;
using Porta.Core.Model;

namespace Porta.App.ViewModels;

/// <summary>Вкладка «Устройства»: доверенные устройства + связывание по токену.</summary>
public partial class DevicesViewModel : ViewModelBase
{
    private readonly IAppData _data;

    public DevicesViewModel(IAppData data)
    {
        _data = data;
        Reload();
    }

    public ObservableCollection<DeviceItem> Items { get; } = [];

    /// <summary>Токен приглашения этого устройства (для QR/копирования).</summary>
    [ObservableProperty]
    public partial string InvitationToken { get; set; } = string.Empty;

    /// <summary>Вставленный токен другого устройства.</summary>
    [ObservableProperty]
    public partial string PastedToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewDeviceName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [RelayCommand]
    private void GenerateInvitation() => InvitationToken = _data.CreateInvitation();

    [RelayCommand]
    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(PastedToken))
            return;

        try
        {
            TrustedDevice device = _data.AcceptInvitation(PastedToken.Trim(), NewDeviceName);
            PastedToken = string.Empty;
            NewDeviceName = string.Empty;
            StatusMessage = $"Устройство «{device.Name}» добавлено";
            Reload();
        }
        catch (FormatException)
        {
            StatusMessage = "Некорректный токен связывания";
        }
    }

    private void Reload()
    {
        Items.Clear();
        foreach (TrustedDevice device in _data.Devices.List())
            Items.Add(new DeviceItem(device.Name, device.Id.ToDisplayString()));
    }
}

/// <summary>Строка списка устройств для отображения.</summary>
public sealed record DeviceItem(string Name, string DeviceId);
