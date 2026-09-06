namespace Porta.Core.Sync;

/// <summary>
/// Повод для синхронизации. Отличает «само сработало» от «человек нажал»: ручные
/// хранилища не должны синхронизироваться сами, но по кнопке — обязаны.
/// См. docs/features/29-managing-what-exists.md.
/// </summary>
public enum SyncTrigger
{
    /// <summary>Пользователь нажал «Синхронизировать».</summary>
    Manual = 0,

    /// <summary>Сработало само: появилось устройство или изменились файлы.</summary>
    Automatic = 1,
}
