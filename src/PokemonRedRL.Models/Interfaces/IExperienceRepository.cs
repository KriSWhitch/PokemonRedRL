using PokemonRedRL.Models.Experience;

public interface IExperienceRepository
{
    Task AddAsync(ModelExperience exp);
    Task<List<ModelExperience>> SampleAsync(int count = -1); // -1 = все данные
    Task<List<ModelExperience>> SamplePrioritizedAsync(int count);
    Task UpdatePrioritiesAsync(Dictionary<Guid, float> updates);
}