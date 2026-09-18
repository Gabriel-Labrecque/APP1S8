using System.Text.Json;
using CoupDeSonde.Api.Models;

namespace CoupDeSonde.Api.Data;

public class FileParticipantRepository
{
    private readonly string _cheminFichier;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // Un seul acces concurrent a la fois : evite une race condition (piege #13) lors
    // de deux inscriptions simultanees avec le meme nom d'utilisateur.
    private static readonly SemaphoreSlim Verrou = new(1, 1);

    public FileParticipantRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _cheminFichier = Path.Combine(dataDirectory, "participants.json");
    }

    public async Task<Participant?> GetByUsernameAsync(string nomUtilisateur)
    {
        var participants = await LireTousAsync();
        return participants.FirstOrDefault(p =>
            string.Equals(p.NomUtilisateur, nomUtilisateur, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> CreateAsync(Participant participant)
    {
        await Verrou.WaitAsync();

        try
        {
            var participants = await LireTousAsync();

            if (participants.Any(p => string.Equals(p.NomUtilisateur, participant.NomUtilisateur, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            participants.Add(participant);

            await using var flux = File.Create(_cheminFichier);
            await JsonSerializer.SerializeAsync(flux, participants, JsonOptions);

            return true;
        }
        finally
        {
            Verrou.Release();
        }
    }

    private async Task<List<Participant>> LireTousAsync()
    {
        if (!File.Exists(_cheminFichier))
        {
            return new List<Participant>();
        }

        await using var flux = File.OpenRead(_cheminFichier);
        var participants = await JsonSerializer.DeserializeAsync<List<Participant>>(flux, JsonOptions);
        return participants ?? new List<Participant>();
    }
}
