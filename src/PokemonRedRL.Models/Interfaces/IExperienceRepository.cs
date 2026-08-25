using PokemonRedRL.Models.Experience;

namespace PokemonRedRL.Models.Interfaces;

public interface IExperienceRepository
{
    Task AddAsync(ModelExperience exp);
    Task<List<ModelExperience>> SampleAsync(int count = -1); // -1 = все данные
    Task<List<ModelExperience>> SamplePrioritizedAsync(int count);
    Task UpdatePrioritiesAsync(Dictionary<Guid, float> updates);
}