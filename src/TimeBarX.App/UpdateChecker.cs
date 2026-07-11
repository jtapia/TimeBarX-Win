using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimeBarX.Core;

namespace TimeBarX.App;

/// <summary>
/// Background HTTP check against a versions.json endpoint. Never installs
/// anything — surfaces an UpdateInfo if a newer version is published so the
/// About tab can prompt the user to download.
/// </summary>
public sealed class UpdateChecker
{
    // versions.json is authored by hand/CI and may use camelCase; bind
    // case-insensitively so a "latestVersion" key doesn't silently bind to null
    // and disable update detection forever.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Uri _endpoint;
    private readonly HttpClient _http;

    public UpdateChecker(Uri endpoint, HttpClient? http = null)
    {
        _endpoint = endpoint;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(_endpoint, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var info = await JsonSerializer.DeserializeAsync<UpdateInfo>(stream, JsonOptions, ct).ConfigureAwait(false);
            if (info is null || string.IsNullOrWhiteSpace(info.LatestVersion)) return null;
            return UpdateInfo.IsNewer(info.LatestVersion, currentVersion) ? info : null;
        }
        catch
        {
            return null;
        }
    }
}
