using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Cardex.Data;
using Cardex.Models;
using Microsoft.EntityFrameworkCore;

namespace Cardex.Services;

// Synchronise la collection (possédées/souhaitées/exclues/favoris/tags/achievements) avec l'API
// Cardex existante, via la clé API fournie par l'utilisateur (distincte de la clé catalogue
// intégrée au build, voir CardexApiService). Transport JSON brut, aucune fusion côté serveur :
// toute la logique de conflit (avertir avant d'écraser, auto-pull) vit côté client, dans
// MainViewModel — ce service ne fait que lire/écrire le blob et l'appliquer tel quel à la DB.
public class CollectionSyncService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly AppSettings _settings;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CollectionSyncService(AppDbContext db, AppSettings settings, string apiKey)
    {
        _db = db;
        _settings = settings;
        // Le AAAA de cardex-api.dev pointe vers une IPv6 injoignable (bug DNS côté hébergement,
        // pas de notre ressort) — sur un réseau avec IPv6 actif, laisser le résolveur par défaut
        // choisir fait pendre la connexion jusqu'au timeout avant de retomber sur IPv4. Forcer la
        // résolution en IPv4 contourne le problème en attendant la correction du DNS.
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(
                    context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cardex-api.dev/api/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<SyncPullResponse?> FetchRemoteStateAsync()
    {
        var response = await _http.GetAsync("sync");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Invalid or unverified Cardex API key.");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SyncPullResponse>(json, _json);
    }

    public async Task<DateTime> PushAsync(string origin)
    {
        var collectionJson = await BuildCollectionJsonAsync();
        var response = await _http.PutAsJsonAsync("sync", new { origin, collectionJson });
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Invalid or unverified Cardex API key.");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<SyncPushResult>(json, _json);
        return result is not null
            ? DateTime.Parse(result.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;
    }

    // Suite à l'incident du 2026-10-15 (un rapatriement automatique silencieux a effacé la
    // collection d'un testeur avec un snapshot vide venu de l'autre appareil), le rapatriement
    // n'est plus jamais appliqué sans confirmation explicite — voir MainViewModel.CheckPendingSyncAsync.
    // Ce compteur sert juste à informer l'utilisateur avant qu'il décide.
    public static int CountOwnedCards(string collectionJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<CollectionSnapshot>(collectionJson, _json);
            return snapshot?.OwnedCards?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<string> BuildCollectionJsonAsync()
    {
        var snapshot = new CollectionSnapshot(
            OwnedCards: (await _db.OwnedCards.ToListAsync())
                .Select(o => new SyncOwnedCard(o.CardId, o.SetId, o.Quantity, o.OwnedAt)).ToList(),
            WantedCards: (await _db.WantedCards.ToListAsync())
                .Select(w => new SyncSimpleCard(w.CardId, w.SetId)).ToList(),
            ExcludedCards: (await _db.ExcludedCards.ToListAsync())
                .Select(x => new SyncSimpleCard(x.CardId, x.SetId)).ToList(),
            FavoriteSets: await _db.FavoriteSets.Select(f => f.SetId).ToListAsync(),
            Tags: (await _db.Tags.ToListAsync())
                .Select(t => new SyncTag(t.Id, t.Name, t.Color)).ToList(),
            CardTags: (await _db.CardTags.ToListAsync())
                .Select(ct => new SyncCardTag(ct.CardId, ct.TagId)).ToList(),
            UnlockedAchievements: (await _db.UnlockedAchievements.ToListAsync())
                .Select(a => new SyncAchievement(a.Id, a.UnlockedAt)).ToList(),
            OwnedCardLanguages: (await _db.OwnedCardLanguages.ToListAsync())
                .Select(l => new SyncOwnedCardLanguage(l.CardId, l.SetId, l.Language, l.Quantity)).ToList(),
            TrackedLanguages: _settings.TrackedLanguages);

        return JsonSerializer.Serialize(snapshot);
    }

    public async Task ApplyCollectionJsonAsync(string collectionJson)
    {
        var snapshot = JsonSerializer.Deserialize<CollectionSnapshot>(collectionJson, _json);
        if (snapshot is null) return;

        await _db.OwnedCards.ExecuteDeleteAsync();
        await _db.OwnedCardLanguages.ExecuteDeleteAsync();
        await _db.WantedCards.ExecuteDeleteAsync();
        await _db.ExcludedCards.ExecuteDeleteAsync();
        await _db.FavoriteSets.ExecuteDeleteAsync();
        await _db.CardTags.ExecuteDeleteAsync();
        await _db.Tags.ExecuteDeleteAsync();
        await _db.UnlockedAchievements.ExecuteDeleteAsync();

        // ExecuteDeleteAsync contourne le change tracker (DELETE direct en SQL) — toute entité
        // déjà suivie sur ces mêmes clés (ex: lue juste avant par BuildCollectionJsonAsync pour le
        // backup pré-sync) reste "trackée" alors que la ligne n'existe plus en base. Le AddRange
        // qui suit créerait un conflit de clé sur ces entités fantômes (exception avalée par le
        // try/catch "best-effort" de l'appelant — l'apply échouait silencieusement à mi-chemin,
        // table vidée mais rien réinséré, d'où le retour à 0 constaté en revisitant un set).
        _db.ChangeTracker.Clear();

        if (snapshot.OwnedCards?.Count > 0)
            _db.OwnedCards.AddRange(snapshot.OwnedCards.Select(o =>
                new OwnedCard { CardId = o.CardId, SetId = o.SetId, Quantity = o.Quantity, OwnedAt = o.OwnedAt }));
        if (snapshot.WantedCards?.Count > 0)
            _db.WantedCards.AddRange(snapshot.WantedCards.Select(w =>
                new WantedCard { CardId = w.CardId, SetId = w.SetId }));
        if (snapshot.ExcludedCards?.Count > 0)
            _db.ExcludedCards.AddRange(snapshot.ExcludedCards.Select(x =>
                new ExcludedCard { CardId = x.CardId, SetId = x.SetId }));
        if (snapshot.FavoriteSets?.Count > 0)
            _db.FavoriteSets.AddRange(snapshot.FavoriteSets.Select(s => new FavoriteSet { SetId = s }));
        if (snapshot.Tags?.Count > 0)
            _db.Tags.AddRange(snapshot.Tags.Select(t => new Tag { Id = t.Id, Name = t.Name, Color = t.Color }));
        if (snapshot.CardTags?.Count > 0)
            _db.CardTags.AddRange(snapshot.CardTags.Select(ct => new CardTag { CardId = ct.CardId, TagId = ct.TagId }));
        if (snapshot.UnlockedAchievements?.Count > 0)
            _db.UnlockedAchievements.AddRange(snapshot.UnlockedAchievements.Select(a =>
                new UnlockedAchievement { Id = a.Id, UnlockedAt = a.UnlockedAt }));
        if (snapshot.OwnedCardLanguages?.Count > 0)
            _db.OwnedCardLanguages.AddRange(snapshot.OwnedCardLanguages.Select(l =>
                new OwnedCardLanguage { CardId = l.CardId, SetId = l.SetId, Language = l.Language, Quantity = l.Quantity }));

        await _db.SaveChangesAsync();

        // Les langues suivies sont synchronisées elles aussi (décision utilisateur) — l'anglais
        // reste toujours forcé présent, même règle que Settings → Languages. Sauvegardé ici plutôt
        // que de compter sur l'appelant : RestoreLastSyncBackupAsync (restauration d'un backup
        // local) n'appelle jamais _settings.Save() après ApplyCollectionJsonAsync.
        var langs = (snapshot.TrackedLanguages ?? ["EN"]).ToHashSet();
        langs.Add("EN");
        _settings.TrackedLanguages = langs.ToList();
        _settings.Save();
    }

    private record SyncPushResult(string UpdatedAt);
}

public record SyncPullResponse(string Origin, string UpdatedAt, string CollectionJson);

public record CollectionSnapshot(
    List<SyncOwnedCard>? OwnedCards,
    List<SyncSimpleCard>? WantedCards,
    List<SyncSimpleCard>? ExcludedCards,
    List<string>? FavoriteSets,
    List<SyncTag>? Tags,
    List<SyncCardTag>? CardTags,
    List<SyncAchievement>? UnlockedAchievements,
    List<SyncOwnedCardLanguage>? OwnedCardLanguages = null,
    List<string>? TrackedLanguages = null);

public record SyncOwnedCard(string CardId, string SetId, int Quantity, DateTime OwnedAt);
public record SyncSimpleCard(string CardId, string SetId);
public record SyncTag(int Id, string Name, string Color);
public record SyncCardTag(string CardId, int TagId);
public record SyncAchievement(string Id, DateTime UnlockedAt);
public record SyncOwnedCardLanguage(string CardId, string SetId, string Language, int Quantity);
