using MessagePack;
using Porta.Core.Identity;

namespace Porta.Core.Sync;

/// <summary>Отношение двух version vectors.</summary>
public enum VectorOrdering
{
    /// <summary>Векторы равны.</summary>
    Identical,

    /// <summary>Этот вектор новее (доминирует).</summary>
    Dominates,

    /// <summary>Другой вектор новее.</summary>
    DominatedBy,

    /// <summary>Параллельные правки — конфликт.</summary>
    Conflict,
}

/// <summary>
/// Version vector: Device ID → счётчик изменений. Позволяет определить отношение версий
/// без доверия к часам. Неизменяемый. См. docs/features/13-version-vectors.md.
/// </summary>
[MessagePackObject]
public sealed class VersionVector : IEquatable<VersionVector>
{
    [Key(0)]
    public IReadOnlyDictionary<string, long> Counters { get; }

    public VersionVector(IReadOnlyDictionary<string, long> counters)
        => Counters = counters;

    /// <summary>Пустой вектор (все счётчики = 0).</summary>
    public static VersionVector Empty { get; } = new(new Dictionary<string, long>());

    /// <summary>Счётчик устройства (0, если отсутствует).</summary>
    public long Get(DeviceId deviceId)
        => Counters.TryGetValue(deviceId.ToString(), out long value) ? value : 0;

    /// <summary>Новый вектор с увеличенным на 1 счётчиком устройства.</summary>
    public VersionVector Increment(DeviceId deviceId)
    {
        var next = new Dictionary<string, long>(Counters, StringComparer.Ordinal);
        string key = deviceId.ToString();
        next[key] = (next.TryGetValue(key, out long value) ? value : 0) + 1;
        return new VersionVector(next);
    }

    /// <summary>Поэлементный максимум двух векторов.</summary>
    public VersionVector Merge(VersionVector other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var merged = new Dictionary<string, long>(Counters, StringComparer.Ordinal);
        foreach ((string key, long value) in other.Counters)
            merged[key] = Math.Max(merged.TryGetValue(key, out long mine) ? mine : 0, value);
        return new VersionVector(merged);
    }

    /// <summary>Определить отношение этого вектора к другому.</summary>
    public VectorOrdering Compare(VersionVector other)
    {
        ArgumentNullException.ThrowIfNull(other);

        bool anyGreater = false;
        bool anyLess = false;

        foreach (string key in Counters.Keys.Union(other.Counters.Keys, StringComparer.Ordinal))
        {
            long mine = Counters.TryGetValue(key, out long a) ? a : 0;
            long theirs = other.Counters.TryGetValue(key, out long b) ? b : 0;
            if (mine > theirs)
                anyGreater = true;
            else if (mine < theirs)
                anyLess = true;
        }

        return (anyGreater, anyLess) switch
        {
            (false, false) => VectorOrdering.Identical,
            (true, false) => VectorOrdering.Dominates,
            (false, true) => VectorOrdering.DominatedBy,
            (true, true) => VectorOrdering.Conflict,
        };
    }

    public bool Equals(VersionVector? other) => other is not null && Compare(other) == VectorOrdering.Identical;

    public override bool Equals(object? obj) => obj is VersionVector other && Equals(other);

    public override int GetHashCode()
    {
        long hash = 17;
        foreach ((string key, long value) in Counters)
            if (value != 0)
                hash += key.GetHashCode(StringComparison.Ordinal) * 31 + value; // порядконезависимо
        return hash.GetHashCode();
    }
}
