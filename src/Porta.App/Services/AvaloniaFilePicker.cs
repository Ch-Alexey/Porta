using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Porta.App.Services;

/// <summary>Системный диалог выбора файлов. См. docs/features/28-drop-ui.md.</summary>
public sealed class AvaloniaFilePicker : IFilePicker
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

    /// <summary>Окно, к которому крепится диалог; null — окна ещё/уже нет.</summary>
    private static TopLevel? ResolveTopLevel() => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
        ISingleViewApplicationLifetime single when single.MainView is not null
            => TopLevel.GetTopLevel(single.MainView),
        _ => null,
    };
}
