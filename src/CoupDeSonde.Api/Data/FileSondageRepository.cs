using System.Collections.Concurrent;
using System.Text.Json;
using CoupDeSonde.Api.Models;

namespace CoupDeSonde.Api.Data;

/// <summary>
/// Persistance simple sur systeme de fichiers (questions et reponses), tel que requis par la problematique.
/// </summary>
public class FileSondageRepository
{
    private readonly string _sondagesDir;
    private readonly string _reponsesDir;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // Une seule ecriture a la fois par sondage : evite une "race condition" (piege #13)
    // si deux participants soumettent une reponse au meme sondage simultanement.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public FileSondageRepository(string dataDirectory)
    {
        _sondagesDir = Path.Combine(dataDirectory, "sondages");
        _reponsesDir = Path.Combine(dataDirectory, "reponses");
        Directory.CreateDirectory(_sondagesDir);
        Directory.CreateDirectory(_reponsesDir);
    }

    public async Task<IReadOnlyList<Sondage>> GetAllAsync()
    {
        var sondages = new List<Sondage>();

        foreach (var fichier in Directory.EnumerateFiles(_sondagesDir, "*.json").OrderBy(f => f))
        {
            var sondage = await LireSondageAsync(fichier);
            if (sondage is not null)
            {
                sondages.Add(sondage);
            }
        }

        return sondages;
    }

    public async Task<Sondage?> GetByIdAsync(int id)
    {
        var chemin = CheminSondage(id);
        return File.Exists(chemin) ? await LireSondageAsync(chemin) : null;
    }

    public async Task<IReadOnlyList<ReponseEnregistree>> GetReponsesAsync(int sondageId)
    {
        var chemin = CheminReponses(sondageId);

        if (!File.Exists(chemin))
        {
            return Array.Empty<ReponseEnregistree>();
        }

        await using var flux = File.OpenRead(chemin);
        var reponses = await JsonSerializer.DeserializeAsync<List<ReponseEnregistree>>(flux, JsonOptions);
        return reponses ?? new List<ReponseEnregistree>();
    }

    /// <summary>
    /// Verifie l'unicite et ecrit la reponse de maniere atomique : la verification et l'ecriture
    /// partagent le meme verrou, ce qui empeche deux requetes simultanees du meme participant de
    /// contourner l'unicite (TOCTOU - piege #13). Retourne false si le participant a deja repondu.
    /// </summary>
    public async Task<bool> EnregistrerReponseSiNouveauAsync(int sondageId, ReponseEnregistree reponse)
    {
        var verrou = Locks.GetOrAdd(sondageId, _ => new SemaphoreSlim(1, 1));
        await verrou.WaitAsync();

        try
        {
            var reponses = (await GetReponsesAsync(sondageId)).ToList();

            if (reponses.Any(r => string.Equals(r.ParticipantId, reponse.ParticipantId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            reponses.Add(reponse);

            var chemin = CheminReponses(sondageId);
            await using var flux = File.Create(chemin);
            await JsonSerializer.SerializeAsync(flux, reponses, JsonOptions);

            return true;
        }
        finally
        {
            verrou.Release();
        }
    }

    private static async Task<Sondage?> LireSondageAsync(string chemin)
    {
        await using var flux = File.OpenRead(chemin);
        return await JsonSerializer.DeserializeAsync<Sondage>(flux, JsonOptions);
    }

    private string CheminSondage(int id) => Path.Combine(_sondagesDir, $"{id}.json");

    private string CheminReponses(int sondageId) => Path.Combine(_reponsesDir, $"{sondageId}.json");
}
