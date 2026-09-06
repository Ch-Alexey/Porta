using Porta.Core.Data;

namespace Porta.Core.Tests.Data;

public class SettingsRepositoryTests
{
    [Fact]
    public void Missing_key_returns_the_fallback()
    {
        using var db = new TempDatabase();
        var settings = new SettingsRepository(db.Database);

        Assert.Equal("/по/умолчанию", settings.Get(SettingKeys.DownloadsFolder, "/по/умолчанию"));
    }

    [Fact]
    public void Stored_value_is_returned()
    {
        using var db = new TempDatabase();
        var settings = new SettingsRepository(db.Database);

        settings.Set(SettingKeys.DownloadsFolder, "/Users/me/Downloads/Porta");

        Assert.Equal("/Users/me/Downloads/Porta", settings.Get(SettingKeys.DownloadsFolder, "/другое"));
    }

    [Fact]
    public void Second_write_overwrites_the_first()
    {
        using var db = new TempDatabase();
        var settings = new SettingsRepository(db.Database);

        settings.Set(SettingKeys.DownloadsFolder, "/первый");
        settings.Set(SettingKeys.DownloadsFolder, "/второй");

        Assert.Equal("/второй", settings.Get(SettingKeys.DownloadsFolder, "/запасной"));
    }

    [Fact]
    public void Value_survives_a_new_repository_over_the_same_database()
    {
        using var db = new TempDatabase();
        new SettingsRepository(db.Database).Set(SettingKeys.DownloadsFolder, "/сохранено");

        Assert.Equal("/сохранено", new SettingsRepository(db.Database).Get(SettingKeys.DownloadsFolder, "/запасной"));
    }

    [Fact]
    public void Empty_key_is_refused()
    {
        using var db = new TempDatabase();
        var settings = new SettingsRepository(db.Database);

        Assert.Throws<ArgumentException>(() => settings.Set("  ", "значение"));
    }
}
