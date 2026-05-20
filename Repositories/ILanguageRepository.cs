using Api_TutorIdiomas.Models;

namespace Api_TutorIdiomas.Repositories
{
    public interface ILanguageRepository
    {
        Task<List<Language>> GetAllAsync();
        Task<Language?> GetByIdAsync(int id);
        Task<Language?> GetByCodeAsync(string code);
    }
}