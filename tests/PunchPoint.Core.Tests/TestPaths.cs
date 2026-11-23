namespace PunchPoint.Core.Tests;

public static class TestPaths
{
    /// <summary>Locates the repository's tests/fixtures folder from the test assembly.</summary>
    public static string Fixtures
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, "tests", "fixtures");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not find tests/fixtures from " + AppContext.BaseDirectory);
        }
    }

    public static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"punchpoint-test-{Guid.NewGuid():n}.db");
}
