using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly Regex GermanRatingPattern = new(
        @"^(?:FSK|DE)[\s\-:]*([0-9]{1,2})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

    public string Name => "Fehlende deutsche FSK-Altersfreigaben ergänzen";
    public string Key => "JFSKUUpdateTask";
    public string Category => "Bibliothek";
    public string Description => "Prüft Filme und Serien ohne erkannte deutsche Freigabe bei TMDb. Deutsche Freigaben bleiben unverändert.";
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning("JFSKU: Kein TMDb API-Key konfiguriert.");
            progress.Report(100);
            return;
        }

        var apiKey = config.TmdbApiKey.Trim();
        var dryRun = config.DryRun;
        var allItems = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
            IsVirtualItem = false,
            Recursive = true
        });

        var candidates = new List<BaseItem>();
        var alreadyGerman = 0;
        var locked = 0;
        var missingId = 0;
        foreach (var item in allItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (HasGermanRating(item.OfficialRating))
            {
                alreadyGerman++;
                continue;
            }
            if (IsRatingLocked(item))
            {
                locked++;
                continue;
            }
            if (!TryGetTmdbId(item, out _))
            {
                missingId++;
                continue;
            }
            candidates.Add(item);
        }

        _logger.LogInformation(
            "JFSKU: {Total} Einträge; {German} mit deutscher Freigabe; {Locked} gesperrt; {MissingId} ohne gültige TMDb-ID; {Candidates} Kandidaten. Testlauf: {DryRun}",
            allItems.Count, alreadyGerman, locked, missingId, candidates.Count, dryRun);

        var current = 0;
        foreach (var item in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Recheck in case metadata changed after candidate selection.
                if (HasGermanRating(item.OfficialRating) || IsRatingLocked(item)
                    || !TryGetTmdbId(item, out var tmdbId))
                {
                    continue;
                }
                await ProcessItemAsync(item, tmdbId, apiKey, dryRun, cancellationToken);
                await Task.Delay(300, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JFSKU: Fehler für {Name}: {ErrorType}", item.Name, ex.GetType().Name);
            }
            finally
            {
                current++;
                progress.Report(100.0 * current / candidates.Count);
            }
        }
        progress.Report(100);
    }

    private static bool IsRatingLocked(BaseItem item) =>
        item.IsLocked || item.LockedFields.Contains(MetadataField.OfficialRating);

    private static bool TryGetTmdbId(BaseItem item, out long id) =>
        long.TryParse(item.GetProviderId(MetadataProvider.Tmdb),
            NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private static bool HasGermanRating(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.Equals("o.A.", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("o. A.", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ohne Altersbeschränkung", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("keine Jugendfreigabe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var match = GermanRatingPattern.Match(normalized);
        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var age)
            && age is 0 or 6 or 12 or 16 or 18;
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
            if (result.GetProperty("iso_3166_1").GetString() != "DE") continue;
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
        if (ratings.Count != 1)
        {
            _logger.LogInformation("JFSKU: {Name} übersprungen: {Count} eindeutige deutsche Freigaben gefunden.",
                item.Name, ratings.Count);
            return;
        }
        if (HasGermanRating(item.OfficialRating) || IsRatingLocked(item)) return;
        var newRating = "FSK-" + ratings.Single().ToString(CultureInfo.InvariantCulture);
        var previous = item.OfficialRating;
        if (dryRun)
        {
            _logger.LogInformation("JFSKU Testlauf: {Name}: {OldRating} -> {NewRating}", item.Name, previous, newRating);
            return;
        }
        item.OfficialRating = newRating;
        try
        {
            await _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken);
        }
        catch
        {
            item.OfficialRating = previous;
            throw;
        }
        _logger.LogInformation("JFSKU: {Name}: {OldRating} -> {NewRating}", item.Name, previous, newRating);
    }

    private static void AddRating(string? value, HashSet<int> ratings)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
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
