using System.Collections.Generic;
using System.Threading.Tasks;

namespace Porta.App.Services;

/// <summary>
/// Выбор файлов пользователем. Абстракция, чтобы view-модель не зависела от Avalonia
/// `IStorageProvider` и тестировалась без окна. См. docs/features/28-drop-ui.md.
/// </summary>
public interface IFilePicker
{
    /// <summary>Открыть диалог выбора. Пустой список — пользователь отменил.</summary>
    Task<IReadOnlyList<string>> PickFilesAsync(string title);
}
