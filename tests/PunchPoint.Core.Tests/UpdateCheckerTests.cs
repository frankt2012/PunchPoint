using PunchPoint.Core.Updates;

namespace PunchPoint.Core.Tests;

public class UpdateCheckerTests
{
    private const string NewerRelease = """
    {
      "tag_name": "v1.5.0",
      "html_url": "https://github.com/frankt2012/PunchPoint/releases/tag/v1.5.0",
      "body": "## What's new\n- check for updates",
      "published_at": "2026-11-24T10:00:00Z",
      "draft": false,
      "prerelease": false,
      "assets": [
        {
          "name": "PunchPoint-Setup-1.5.0.exe",
          "browser_download_url": "https://github.com/frankt2012/PunchPoint/releases/download/v1.5.0/PunchPoint-Setup-1.5.0.exe",
          "size": 148894829
        }
      ]
    }
    """;

    [Fact]
    public void Parses_tags()
    {
        Assert.Equal(new Version(1, 5, 0, 0), UpdateChecker.ParseTag("v1.5.0"));
        Assert.Equal(new Version(1, 5, 0, 0), UpdateChecker.ParseTag("1.5"));
        Assert.Equal(new Version(1, 5, 0, 0), UpdateChecker.ParseTag("v1.5.0-beta.2"));
        Assert.Equal(new Version(10, 0, 3, 0), UpdateChecker.ParseTag("V10.0.3"));
        Assert.Null(UpdateChecker.ParseTag("nightly"));
        Assert.Null(UpdateChecker.ParseTag(""));
    }

    [Fact]
    public void Normalises_repository_names()
    {
        Assert.Equal("frankt2012/PunchPoint", UpdateChecker.NormaliseRepo("frankt2012/PunchPoint"));
        Assert.Equal("frankt2012/PunchPoint", UpdateChecker.NormaliseRepo("https://github.com/frankt2012/PunchPoint"));
        Assert.Equal("frankt2012/PunchPoint", UpdateChecker.NormaliseRepo("https://github.com/frankt2012/PunchPoint.git"));
        Assert.Equal("someone/thing", UpdateChecker.NormaliseRepo("  someone/thing/ "));
        Assert.Null(UpdateChecker.NormaliseRepo("PunchPoint"));
        Assert.Null(UpdateChecker.NormaliseRepo(""));
    }

    [Fact]
    public void Finds_a_newer_release_with_its_notes_and_installer()
    {
        var info = UpdateChecker.Parse(NewerRelease, new Version(1, 4, 0));

        Assert.NotNull(info);
        Assert.Equal(new Version(1, 5, 0, 0), info!.Version);
        Assert.Equal("1.5.0", info.Display);
        Assert.Equal("v1.5.0", info.Tag);
        Assert.Contains("check for updates", info.Notes);
        Assert.Equal("https://github.com/frankt2012/PunchPoint/releases/tag/v1.5.0", info.ReleaseUrl);
        Assert.NotNull(info.Installer);
        Assert.Contains("PunchPoint-Setup-1.5.0.exe", info.Installer!.Name);
    }

    [Fact]
    public void Ignores_the_same_or_an_older_version()
    {
        Assert.Null(UpdateChecker.Parse(NewerRelease, new Version(1, 5, 0)));
        Assert.Null(UpdateChecker.Parse(NewerRelease, new Version(1, 6, 0)));
    }

    [Fact]
    public void Ignores_drafts_prereleases_and_garbage()
    {
        Assert.Null(UpdateChecker.Parse(NewerRelease.Replace("\"draft\": false", "\"draft\": true"),
            new Version(1, 4, 0)));
        Assert.Null(UpdateChecker.Parse(NewerRelease.Replace("\"prerelease\": false", "\"prerelease\": true"),
            new Version(1, 4, 0)));
        Assert.Null(UpdateChecker.Parse("not json", new Version(1, 4, 0)));
        Assert.Null(UpdateChecker.Parse("""{"tag_name":"nonsense"}""", new Version(1, 4, 0)));
    }

    [Fact]
    public void Settings_round_trip_update_preferences()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            using var database = Storage.Database.Open(path);
            var store = new Storage.Store(database);

            var settings = store.GetSettings();
            Assert.True(settings.CheckForUpdates);
            Assert.Equal(UpdateChecker.DefaultRepo, settings.UpdateRepo);

            settings.CheckForUpdates = false;
            settings.UpdateRepo = "someone/fork";
            settings.LastUpdateCheck = new DateTime(2026, 11, 24, 8, 30, 0);
            settings.SkippedUpdateVersion = "1.5.0";
            store.SaveSettings(settings);

            var reloaded = store.GetSettings();
            Assert.False(reloaded.CheckForUpdates);
            Assert.Equal("someone/fork", reloaded.UpdateRepo);
            Assert.Equal(new DateTime(2026, 11, 24, 8, 30, 0), reloaded.LastUpdateCheck);
            Assert.Equal("1.5.0", reloaded.SkippedUpdateVersion);
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }
}
