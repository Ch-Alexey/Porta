using Porta.Core.Data;
using Porta.Core.Model;

namespace Porta.Core.App;

/// <summary>
/// Данные и операции приложения для UI: личность, репозитории и связывание через
/// абстракции, чтобы view-модели тестировались без файловой системы.
/// См. docs/features/18-app-shell.md.
/// </summary>
public interface IAppData
{
    string DeviceName { get; }
    string DeviceId { get; }
    IStorageRepository Storages { get; }
    IDeviceRepository Devices { get; }

    /// <summary>Сгенерировать приглашение (токен для QR/ссылки) для связывания.</summary>
    string CreateInvitation();

    /// <summary>
    /// Принять приглашение: разобрать токен, добавить инициатора в доверенные.
    /// Бросает <see cref="System.FormatException"/> при некорректном токене.
    /// </summary>
    TrustedDevice AcceptInvitation(string token, string deviceName);
}
