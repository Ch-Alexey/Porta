using Porta.Core.Identity;

namespace Porta.Core.Model;

/// <summary>
/// Хранилище — папка, участвующая в синхронизации. ID общий на всех устройствах, где
/// оно подключено; локальный путь может отличаться. См. docs/features/02-data-model.md.
/// </summary>
/// <param name="Id">ID хранилища (общий между устройствами).</param>
/// <param name="Name">Имя хранилища.</param>
/// <param name="LocalPath">Путь к папке на этом устройстве.</param>
/// <param name="ExchangeMode">Режим обмена.</param>
/// <param name="SyncMode">Авто или ручная синхронизация.</param>
/// <param name="Paused">Приостановлено ли.</param>
/// <param name="CreatedAt">Когда создано.</param>
public sealed record Storage(
    string Id,
    string Name,
    string LocalPath,
    StorageExchangeMode ExchangeMode,
    SyncMode SyncMode,
    bool Paused,
    DateTimeOffset CreatedAt);

/// <summary>
/// Связь хранилища с доверенным устройством, с которым оно синхронизируется.
/// </summary>
/// <param name="StorageId">ID хранилища.</param>
/// <param name="DeviceId">Device ID устройства.</param>
/// <param name="AutoSync">Настроена ли авто-синхро пара для этого хранилища.</param>
public sealed record StorageDeviceLink(
    string StorageId,
    DeviceId DeviceId,
    bool AutoSync);
