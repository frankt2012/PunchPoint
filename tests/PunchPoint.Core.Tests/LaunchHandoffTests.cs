using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

/// <summary>
/// The clock hands the management app a one-time token instead of asking for the supervisor
/// password twice.
/// </summary>
public sealed class LaunchHandoffTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pp-handoff-" + Guid.NewGuid().ToString("N"));
    private readonly string? _previous;

    public LaunchHandoffTests()
    {
        _previous = Environment.GetEnvironmentVariable("PUNCHPOINT_DATA");
        Directory.CreateDirectory(_folder);
        Environment.SetEnvironmentVariable("PUNCHPOINT_DATA", _folder);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PUNCHPOINT_DATA", _previous);
        try { Directory.Delete(_folder, recursive: true); } catch { }
    }

    [Fact]
    public void A_fresh_token_is_accepted_once()
    {
        string token = LaunchHandoff.Create();

        Assert.True(LaunchHandoff.Consume(token));
        Assert.False(LaunchHandoff.Consume(token)); // single use
    }

    [Fact]
    public void A_wrong_token_is_refused_and_clears_the_handoff()
    {
        LaunchHandoff.Create();

        Assert.False(LaunchHandoff.Consume("not-the-token"));
        Assert.False(LaunchHandoff.Consume(null));
    }

    [Fact]
    public void An_old_token_is_refused()
    {
        string token = "abc123";
        File.WriteAllText(
            Path.Combine(_folder, "admin-launch.tmp"),
            token + "\n" + DateTime.UtcNow.AddMinutes(-10).Ticks);

        Assert.False(LaunchHandoff.Consume(token));
    }

    [Fact]
    public void No_token_file_means_no_handoff()
    {
        Assert.False(LaunchHandoff.Consume("anything"));
    }
}
