namespace Porta.Core.Sync;

/// <summary>
/// Сигнал «в наблюдаемых хранилищах что-то изменилось» (уже дебаунснутый). Абстракция,
/// чтобы ядро не зависело от FileSystemWatcher. См. docs/features/23-file-change-sync.md.
/// </summary>
public interface IChangeNotifier
{
    event Action Changed;
}
