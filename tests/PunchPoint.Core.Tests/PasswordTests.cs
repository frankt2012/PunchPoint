using PunchPoint.Core.Services;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

public class PasswordTests
{
    [Fact]
    public void Hash_is_not_the_password_and_verifies()
    {
        string hash = PasswordHasher.Hash("correct horse battery");

        Assert.DoesNotContain("correct horse battery", hash);
        Assert.StartsWith("pbkdf2$", hash);
        Assert.True(PasswordHasher.Verify("correct horse battery", hash));
        Assert.False(PasswordHasher.Verify("wrong", hash));
        Assert.False(PasswordHasher.Verify("", hash));
    }

    [Fact]
    public void Same_password_gives_different_hashes()
    {
        string a = PasswordHasher.Hash("secret1");
        string b = PasswordHasher.Hash("secret1");

        Assert.NotEqual(a, b); // random salt
        Assert.True(PasswordHasher.Verify("secret1", a));
        Assert.True(PasswordHasher.Verify("secret1", b));
    }

    [Fact]
    public void Rejects_missing_or_malformed_hashes()
    {
        Assert.False(PasswordHasher.Verify("x", null));
        Assert.False(PasswordHasher.Verify("x", ""));
        Assert.False(PasswordHasher.Verify("x", "not-a-hash"));
        Assert.False(PasswordHasher.Verify("x", "pbkdf2$abc$zzz$zzz"));
        Assert.True(PasswordHasher.LooksHashed(PasswordHasher.Hash("x")));
    }

    [Fact]
    public void Settings_round_trip_the_password_hash()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            var settings = store.GetSettings();
            Assert.Equal("", settings.AdminPasswordHash);

            settings.AdminPasswordHash = PasswordHasher.Hash("1234");
            store.SaveSettings(settings);

            var reloaded = store.GetSettings();
            Assert.True(PasswordHasher.Verify("1234", reloaded.AdminPasswordHash));
            Assert.False(PasswordHasher.Verify("4321", reloaded.AdminPasswordHash));
            Assert.True(Schema.CurrentVersion >= 2);
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(dbPath + suffix); } catch { }
            }
        }
    }
}

public class ClockSettingsTests
{
    [Fact]
    public void Clock_window_mode_round_trips()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            var settings = store.GetSettings();
            Assert.False(settings.ClockFullScreen); // windowed by default

            settings.ClockFullScreen = true;
            settings.ClockWidth = 1200;
            settings.ClockHeight = 800;
            store.SaveSettings(settings);

            var reloaded = store.GetSettings();
            Assert.True(reloaded.ClockFullScreen);
            Assert.Equal(1200, reloaded.ClockWidth);
            Assert.Equal(800, reloaded.ClockHeight);
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(dbPath + suffix); } catch { }
            }
        }
    }
}

public class ThemeSettingTests
{
    [Fact]
    public void Theme_round_trips_and_defaults_to_system()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            Assert.Equal(Core.Models.AppTheme.System, store.GetSettings().Theme);

            var settings = store.GetSettings();
            settings.Theme = Core.Models.AppTheme.Dark;
            store.SaveSettings(settings);

            Assert.Equal(Core.Models.AppTheme.Dark, store.GetSettings().Theme);
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(dbPath + suffix); } catch { }
            }
        }
    }
}
