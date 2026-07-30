namespace Porta.Core.Model;

/// <summary>Режим обмена для хранилища.</summary>
public enum StorageExchangeMode
{
    /// <summary>Двусторонняя синхронизация.</summary>
    TwoWay = 0,

    /// <summary>Только приём изменений с других устройств.</summary>
    ReceiveOnly = 1,

    /// <summary>Только отдача своих изменений.</summary>
    SendOnly = 2,
}

/// <summary>Как запускается синхронизация хранилища.</summary>
public enum SyncMode
{
    /// <summary>Автоматически при изменениях.</summary>
    Automatic = 0,

    /// <summary>Вручную по команде пользователя.</summary>
    Manual = 1,
}
