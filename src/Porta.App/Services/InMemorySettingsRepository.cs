using System;
using System.Collections.Generic;
using Porta.Core.Data;

namespace Porta.App.Services;

/// <summary>
/// Настройки без персистентности — запасной вариант, когда голова не внедрила
/// настоящий репозиторий (design-time, тесты). Значения живут до закрытия приложения.
/// </summary>
public sealed class InMemorySettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public string Get(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

    public void Set(string key, string value) => _values[key] = value;
}
