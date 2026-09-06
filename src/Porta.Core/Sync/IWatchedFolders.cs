namespace Porta.Core.Sync;

/// <summary>
/// Набор папок, за которыми следит наблюдатель изменений. Узкая абстракция, чтобы UI мог
/// сказать «список хранилищ поменялся», не зная про `FileSystemWatcher`.
/// См. docs/features/29-managing-what-exists.md.
/// </summary>
public interface IWatchedFolders
{
    /// <summary>Наблюдать ровно за этими папками (старые закрываются).</summary>
    void Reconfigure(IEnumerable<string> folders);
}
