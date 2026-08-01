using System;
using Avalonia.Threading;

namespace Porta.App.Services;

/// <summary>Постановка действия в UI-поток (абстракция для тестируемости view-моделей).</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

/// <summary>Диспетчер поверх Avalonia UI-потока.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
