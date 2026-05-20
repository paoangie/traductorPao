using Api_TutorIdiomas.Models;

namespace Api_TutorIdiomas.Repositories
{
    public interface IMistakeRepository
    {
        Task<List<UserMistake>> GetByUserAndLanguageAsync(Guid userId, int languageId);
        Task<List<UserMistake>> GetCommonMistakesAsync(Guid userId, int languageId, int limit = 5);
        Task AddOrUpdateAsync(UserMistake mistake);
        Task SaveChangesAsync();
    }
}
