using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Porta.App.Services;

/// <summary>
/// Системные диалоги выбора файлов и папки.
/// См. docs/features/28-drop-ui.md и 29-managing-what-exists.md.
/// </summary>
public sealed class AvaloniaFilePicker : IFilePicker, IFolderPicker
{
    public async Task<IReadOnlyList<string>> PickFilesAsync(string title)
    {
        TopLevel? top = ResolveTopLevel();
        if (top is null)
            return [];

        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = title, AllowMultiple = true });

        return files
            .Select(f => f.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .ToList();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        TopLevel? top = ResolveTopLevel();
        if (top is null)
            return null;

        IReadOnlyList<IStorageFolder> folders = await top.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    /// <summary>Окно, к которому крепится диалог; null — окна ещё/уже нет.</summary>
    private static TopLevel? ResolveTopLevel() => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
        ISingleViewApplicationLifetime single when single.MainView is not null
            => TopLevel.GetTopLevel(single.MainView),
        _ => null,
    };
}
