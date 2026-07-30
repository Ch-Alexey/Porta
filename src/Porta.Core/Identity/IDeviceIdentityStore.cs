namespace Porta.Core.Identity;

/// <summary>
/// Хранилище личности устройства. Абстракция, чтобы позже заменить файловое хранение
/// на платформенное защищённое (DPAPI/Keychain/Keystore), не трогая остальной код.
/// </summary>
public interface IDeviceIdentityStore
{
    /// <summary>
    /// Загрузить существующую личность или создать новую (и сохранить) при первом запуске.
    /// </summary>
    DeviceIdentity LoadOrCreate();
}
