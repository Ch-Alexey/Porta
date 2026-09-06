using System.Threading.Tasks;

namespace Porta.App.Services;

/// <summary>
/// Выбор папки пользователем. Отдельно от <see cref="IFilePicker"/>: у системных
/// диалогов это разные вызовы. См. docs/features/29-managing-what-exists.md.
/// </summary>
public interface IFolderPicker
{
    /// <summary>Открыть диалог выбора папки. <c>null</c> — пользователь отменил.</summary>
    Task<string?> PickFolderAsync(string title);
}
