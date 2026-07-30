using System.Runtime.InteropServices;

namespace Porta.Core.Identity;

/// <summary>
/// Файловое хранилище личности: приватный ключ (PKCS#8) в одном файле.
/// ВРЕМЕННО — на POSIX-системах файл ограничивается правами 0600; платформенное
/// защищённое хранилище подключим позже через <see cref="IDeviceIdentityStore"/>.
/// См. docs/features/01-identity.md.
/// </summary>
public sealed class FileDeviceIdentityStore : IDeviceIdentityStore
{
    private readonly string _path;

    /// <param name="path">Путь к файлу с приватным ключом устройства.</param>
    public FileDeviceIdentityStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public DeviceIdentity LoadOrCreate()
    {
        if (File.Exists(_path))
        {
            byte[] existing = File.ReadAllBytes(_path);
            return DeviceIdentity.ImportPrivateKey(existing);
        }

        var identity = DeviceIdentity.Generate();

        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(_path, identity.ExportPrivateKey());
        RestrictToOwner(_path);

        return identity;
    }

    private static void RestrictToOwner(string path)
    {
        // На Windows права наследуются от папки профиля; строгий ACL — задача на будущее.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException)
        {
            // Файловая система без POSIX-прав — пропускаем.
        }
    }
}
