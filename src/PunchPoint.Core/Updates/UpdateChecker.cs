using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace PunchPoint.Core.Updates;

public enum UpdateStatus
{
    UpToDate,
    Available,
    NoReleases,
    Failed,
}

public sealed record UpdateAsset(string Name, string Url, long Size);

public sealed record UpdateInfo(
    Version Version,
    string Tag,
    string ReleaseUrl,
    string Notes,
    DateTimeOffset? Published,
    IReadOnlyList<UpdateAsset> Assets)
{
    /// <summary>"1.5.0" rather than "1.5.0.0".</summary>
    public string Display => Version.Revision > 0 ? Version.ToString(4) : Version.ToString(3);

    /// <summary>The Windows installer attached to the release, if there is one.</summary>
    public UpdateAsset? Installer =>
        Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
}

public sealed record UpdateResult(UpdateStatus Status, UpdateInfo? Update, string Message)
{
    public bool HasUpdate => Status == UpdateStatus.Available;
}

/// <summary>
/// Reads the latest published release from GitHub. Read-only: one HTTPS GET, no credentials,
/// no data sent beyond a user agent, and no automatic download or install.
/// </summary>
public static class UpdateChecker
{
    public const string DefaultRepo = "frankt2012/PunchPoint";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PunchPoint-UpdateCheck");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<UpdateResult> CheckAsync(string? repo, Version current, CancellationToken token = default)
    {
        string slug = NormaliseRepo(repo) ?? DefaultRepo;

        try
        {
            using var response = await Http.GetAsync(
                $"https://api.github.com/repos/{slug}/releases/latest", token);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UpdateResult(UpdateStatus.NoReleases, null,
                    "There are no published releases yet.");

            if (!response.IsSuccessStatusCode)
                return new UpdateResult(UpdateStatus.Failed, null,
                    $"GitHub answered {(int)response.StatusCode}. Try again later.");

            string json = await response.Content.ReadAsStringAsync(token);
            var info = Parse(json, current);

            return info is null
                ? new UpdateResult(UpdateStatus.UpToDate, null, $"This is the latest version ({current}).")
                : new UpdateResult(UpdateStatus.Available, info, $"Version {info.Display} is available.");
        }
        catch (OperationCanceledException)
        {
            return new UpdateResult(UpdateStatus.Failed, null, "The check timed out.");
        }
        catch (Exception ex)
        {
            return new UpdateResult(UpdateStatus.Failed, null, "Could not reach GitHub: " + ex.Message);
        }
    }

    /// <summary>Returns the release only when it is newer than <paramref name="current"/>.</summary>
    public static UpdateInfo? Parse(string json, Version current)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;

            string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var version = ParseTag(tag);
            if (version is null) return null;
            if (version <= Normalise(current)) return null;

            string url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

            DateTimeOffset? published = null;
            if (root.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(p.GetString(), out var when))
            {
                published = when;
            }

            var assets = new List<UpdateAsset>();
            if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in list.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string link = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() ?? "" : "";
                    long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out long bytes) ? bytes : 0;
                    if (name.Length > 0 && link.Length > 0)
                        assets.Add(new UpdateAsset(name, link, size));
                }
            }

            return new UpdateInfo(version, tag, url, notes, published, assets);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Accepts "1.5.0", "v1.5.0", "1.5", or "v1.5.0-beta.2".</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        string text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];

        int cut = text.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut > 0) text = text[..cut];

        return Version.TryParse(text, out var version) ? Normalise(version) : null;
    }

    /// <summary>Accepts "owner/repo", a GitHub URL, or a trailing .git.</summary>
    public static string? NormaliseRepo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string text = value.Trim();

        if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            text = uri.AbsolutePath.Trim('/');
        }

        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            text = text[..^4];

        string[] parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        string owner = parts[^2];
        string name = parts[^1];
        if (owner.Length == 0 || name.Length == 0) return null;

        return $"{owner}/{name}";
    }

    private static Version Normalise(Version version) => new(
        version.Major,
        version.Minor,
        version.Build < 0 ? 0 : version.Build,
        version.Revision < 0 ? 0 : version.Revision);
}
