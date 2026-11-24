using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace Grand_Theft_Auto_V_Platform_Switcher.Services;

public sealed record UpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string Message);

public sealed class UpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/boncabee/PlatformSwitcherGTAV/releases/latest";
    private const string FallbackReleasesUrl = "https://github.com/boncabee/PlatformSwitcherGTAV/releases";

    private readonly AppLogger _logger;
    private readonly HttpClient _httpClient;

    public UpdateService(AppLogger logger, HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? CreateDefaultClient();
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("PlatformSwitcherGTAV", "2.0.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVer = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
        try
        {
            using var response = await _httpClient.GetAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    false,
                    false,
                    currentVer,
                    currentVer,
                    FallbackReleasesUrl,
                    $"Could not reach GitHub Releases (HTTP {(int)response.StatusCode}).");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
            var htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? FallbackReleasesUrl : FallbackReleasesUrl;

            var cleanLatest = tagName.Trim().TrimStart('v', 'V');
            var newer = IsNewerVersion(currentVer, cleanLatest);

            return new UpdateCheckResult(
                true,
                newer,
                currentVer,
                string.IsNullOrWhiteSpace(cleanLatest) ? currentVer : cleanLatest,
                htmlUrl,
                newer
                    ? $"A new version (v{cleanLatest}) is available! You are running v{currentVer}."
                    : $"You are up to date (v{currentVer}).");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Update check failed: {ex.Message}");
            return new UpdateCheckResult(
                false,
                false,
                currentVer,
                currentVer,
                FallbackReleasesUrl,
                $"Update check failed: {ex.Message}");
        }
    }

    internal static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        if (!TryNormalizeVersion(currentVersion, out var current) ||
            !TryNormalizeVersion(latestVersion, out var latest))
        {
            return false;
        }

        return latest > current;
    }

    private static bool TryNormalizeVersion(string raw, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim().TrimStart('v', 'V').Split('-')[0];
        return Version.TryParse(cleaned, out version!);
    }
}
