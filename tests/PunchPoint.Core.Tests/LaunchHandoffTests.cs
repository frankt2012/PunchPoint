using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

/// <summary>
/// The clock hands the management app a one-time token instead of asking for the supervisor
/// password twice.
/// </summary>
public sealed class LaunchHandoffTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pp-handoff-" + Guid.NewGuid().ToString("N"));

    public LaunchHandoffTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { }
    }

    [Fact]
    public void A_fresh_token_is_accepted_once()
    {
        string token = LaunchHandoff.Create(_folder);

        Assert.True(LaunchHandoff.Consume(token, _folder));
        Assert.False(LaunchHandoff.Consume(token, _folder)); // single use
    }

    [Fact]
    public void A_wrong_token_is_refused_and_clears_the_handoff()
    {
        LaunchHandoff.Create(_folder);

        Assert.False(LaunchHandoff.Consume("not-the-token", _folder));
        Assert.False(LaunchHandoff.Consume(null, _folder));
    }

    [Fact]
    public void An_old_token_is_refused()
    {
        string token = "abc123";
        File.WriteAllText(
            Path.Combine(_folder, "admin-launch.tmp"),
            token + "\n" + DateTime.UtcNow.AddMinutes(-10).Ticks);

        Assert.False(LaunchHandoff.Consume(token, _folder));
    }

    [Fact]
    public void No_token_file_means_no_handoff()
    {
        Assert.False(LaunchHandoff.Consume("anything", _folder));
    }
}
