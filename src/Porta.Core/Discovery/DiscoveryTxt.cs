using Porta.Core.Identity;

namespace Porta.Core.Discovery;

/// <summary>
/// Чистый кодек TXT-записей DNS-SD для Porta. Формат: <c>id=…</c>, <c>name=…</c>,
/// <c>v=…</c>. См. docs/features/05-discovery.md.
/// </summary>
public static class DiscoveryTxt
{
    /// <summary>Тип DNS-SD сервиса Porta.</summary>
    public const string ServiceType = "_porta._tcp";

    /// <summary>Версия формата discovery.</summary>
    public const int Version = 1;

    private const string IdKey = "id";
    private const string NameKey = "name";
    private const string VersionKey = "v";

    /// <summary>Имя инстанса DNS-SD = Device ID (уникальность в сети).</summary>
    public static string InstanceName(DeviceId deviceId) => deviceId.ToString();

    /// <summary>Собрать TXT-записи как пары ключ/значение (для mDNS-стека).</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> BuildPairs(PortaAdvertisement advertisement)
    {
        ArgumentNullException.ThrowIfNull(advertisement);
        return
        [
            new(IdKey, advertisement.DeviceId.ToString()),
            new(NameKey, advertisement.Name),
            new(VersionKey, Version.ToString()),
        ];
    }

    /// <summary>Собрать TXT-записи как строки <c>key=value</c>.</summary>
    public static IReadOnlyList<string> Build(PortaAdvertisement advertisement)
        => BuildPairs(advertisement).Select(p => $"{p.Key}={p.Value}").ToArray();

    /// <summary>
    /// Разобрать TXT-записи. Возвращает <c>false</c>, если нет/битый <c>id</c> или
    /// версия отсутствует/не поддерживается.
    /// </summary>
    public static bool TryParse(
        IEnumerable<string> txt,
        out DeviceId? deviceId,
        out string name,
        out int version)
    {
        deviceId = null;
        name = string.Empty;
        version = 0;

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string record in txt)
        {
            int sep = record.IndexOf('=');
            if (sep <= 0)
                continue;
            string key = record[..sep];
            string value = record[(sep + 1)..];
            map[key] = value; // при дублях побеждает последняя запись
        }

        if (!map.TryGetValue(VersionKey, out string? versionRaw)
            || !int.TryParse(versionRaw, out version)
            || version != Version)
            return false;

        if (!map.TryGetValue(IdKey, out string? idRaw)
            || !DeviceId.TryParse(idRaw, out deviceId))
            return false;

        name = map.GetValueOrDefault(NameKey, string.Empty);
        return true;
    }
}
