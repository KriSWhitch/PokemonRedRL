using System.Text.Json;
using PokemonRedRL.DAL.Models;

namespace PokemonRedRL.Utils.Helpers;

public class GameStateSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public GameStateResponse Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<GameStateResponse>(json, _options)
                   ?? throw new NullReferenceException();
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Invalid JSON: {ex.Message}");
        }
    }
}
