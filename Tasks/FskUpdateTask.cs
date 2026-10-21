using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JFSKU.Tasks;

public sealed class FskUpdateTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FskUpdateTask> _logger;

    public FskUpdateTask(ILibraryManager libraryManager,
        IHttpClientFactory httpClientFactory, ILogger<FskUpdateTask> logger)
    {
        _libraryManager = libraryManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "FSK-Altersfreigaben aktualisieren";
    public string Key => "JFSKUUpdateTask";
    public string Category => "Bibliothek";
    public string Description => "Ergänzt deutsche TMDb-Altersfreigaben für Filme und Serien.";

    // No automatic writes before the administrator has reviewed the settings.
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning("JFSKU: Kein TMDb API-Key konfiguriert.");
            progress.Report(100);
            return;
        }

        var apiKey = config.TmdbApiKey.Trim();
        var overwrite = config.OverwriteExisting;
        var dryRun = config.DryRun;
        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
            IsVirtualItem = false,
            Recursive = true
        });

        progress.Report(0);
        var current = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (item.IsLocked || item.LockedFields.Contains(MetadataField.OfficialRating))
                {
                    continue;
                }

                if (!overwrite && !string.IsNullOrWhiteSpace(item.OfficialRating))
                {
                    continue;
                }

                var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);
                if (long.TryParse(tmdbId, out var numericId) && numericId > 0)
                {
                    await ProcessItemAsync(item, numericId, apiKey, dryRun, cancellationToken);
                    await Task.Delay(300, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Do not log request URLs or API keys.
                _logger.LogWarning("JFSKU: Fehler für {Name}: {ErrorType}", item.Name, ex.GetType().Name);
            }
            finally
            {
                current++;
                progress.Report(100.0 * current / items.Count);
            }
        }

        progress.Report(100);
    }

    private async Task ProcessItemAsync(BaseItem item, long tmdbId, string apiKey,
        bool dryRun, CancellationToken cancellationToken)
    {
        var isMovie = item is Movie;
        var path = isMovie ? $"movie/{tmdbId}/release_dates" : $"tv/{tmdbId}/content_ratings";
        var client = _httpClientFactory.CreateClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await client.GetAsync(
            $"https://api.themoviedb.org/3/{path}?api_key={Uri.EscapeDataString(apiKey)}", timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("JFSKU: TMDb HTTP {Status} für {Name}", (int)response.StatusCode, item.Name);
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        var ratings = new HashSet<int>();
        foreach (var result in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            if (result.GetProperty("iso_3166_1").GetString() != "DE")
            {
                continue;
            }

            if (isMovie)
            {
                foreach (var release in result.GetProperty("release_dates").EnumerateArray())
                {
                    AddRating(release.GetProperty("certification").GetString(), ratings);
                }
            }
            else
            {
                AddRating(result.GetProperty("rating").GetString(), ratings);
            }
        }

        // Conflicting editions cannot be assigned safely without edition metadata.
        if (ratings.Count != 1)
        {
            if (ratings.Count > 1)
            {
                _logger.LogWarning("JFSKU: Widersprüchliche deutsche Freigaben für {Name}; übersprungen.", item.Name);
            }
            return;
        }

        var rating = "FSK-" + ratings.Single().ToString(CultureInfo.InvariantCulture);
        if (item.OfficialRating == rating)
        {
            return;
        }

        if (dryRun)
        {
            _logger.LogInformation("JFSKU Testlauf: {Name}: {OldRating} -> {NewRating}",
                item.Name, item.OfficialRating, rating);
            return;
        }

        var previous = item.OfficialRating;
        item.OfficialRating = rating;
        try
        {
            await _libraryManager.UpdateItemAsync(item, item.GetParent(),
                ItemUpdateType.MetadataEdit, cancellationToken);
        }
        catch
        {
            item.OfficialRating = previous;
            throw;
        }

        _logger.LogInformation("JFSKU: {Name}: {OldRating} -> {NewRating}", item.Name, previous, rating);
    }

    private static void AddRating(string? value, HashSet<int> ratings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("FSK", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..].TrimStart(' ', '-', ':');
        }

        if (int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var age)
            && age is 0 or 6 or 12 or 16 or 18)
        {
            ratings.Add(age);
        }
    }
}
