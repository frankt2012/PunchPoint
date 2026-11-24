namespace PunchPoint.Core.Storage;

/// <summary>
/// Lets the clock open the management app without asking for the password twice.
///
/// The clock has already checked the supervisor password, so it stores a one-time token in the
/// data folder and passes it on the command line; the app accepts it for two minutes and deletes
/// it. The token lives beside the database, which only administrators can write to — and anyone
/// who can write there could edit the database directly anyway.
/// </summary>
public static class LaunchHandoff
{
    private static string TokenPath => Path.Combine(AppPaths.DataDirectory, "admin-launch.tmp");

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>Creates a token, stores it and returns it for the command line.</summary>
    public static string Create()
    {
        AppPaths.EnsureDataDirectory();
        string token = Guid.NewGuid().ToString("N");
        File.WriteAllText(TokenPath, token + "\n" + DateTime.UtcNow.Ticks);
        return token;
    }

    /// <summary>True when the token matches one this machine created in the last two minutes.</summary>
    public static bool Consume(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            if (!File.Exists(TokenPath)) return false;

            string[] lines = File.ReadAllLines(TokenPath);
            File.Delete(TokenPath); // single use, whatever happens

            if (lines.Length < 2) return false;
            if (!long.TryParse(lines[1], out long ticks)) return false;
            if (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) > Lifetime) return false;

            return string.Equals(lines[0], token.Trim(), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
