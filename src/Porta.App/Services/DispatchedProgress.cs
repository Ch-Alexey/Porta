using System;

namespace Porta.App.Services;

/// <summary>
/// Отчёты о ходе работы, доставляемые через <see cref="IUiDispatcher"/>.
/// Отдельно от <c>Progress&lt;T&gt;</c>: тот привязывается к SynchronizationContext и
/// доставляет асинхронно, а в проекте маршалингом в UI-поток уже занимается диспетчер —
/// одного механизма достаточно, и он тестируем.
/// См. docs/features/33-progress-and-cancel.md.
/// </summary>
public sealed class DispatchedProgress<T>(IUiDispatcher dispatcher, Action<T> handler) : IProgress<T>
{
    public void Report(T value) => dispatcher.Post(() => handler(value));
}
