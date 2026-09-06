namespace Porta.Core.Sync;

/// <summary>
/// Разрешение относительного пути внутри папки с защитой от выхода за её пределы
/// (пути вида «../..» приходят из сети). См. docs/features/26-drop-transfer.md.
/// </summary>
public static class SafePath
{
    /// <summary>
    /// Полный путь для <paramref name="relativePath"/> внутри <paramref name="root"/>.
    /// Бросает <see cref="InvalidOperationException"/>, если путь выводит наружу.
    /// </summary>
    public static string Resolve(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(relativePath);

        string rootFull = Path.GetFullPath(root);
        string full = Path.GetFullPath(Path.Combine(rootFull, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;

        if (full != rootFull && !full.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Небезопасный путь вне папки: {relativePath}");

        return full;
    }
}
