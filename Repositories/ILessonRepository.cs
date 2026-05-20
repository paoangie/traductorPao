using Api_TutorIdiomas.Models;

namespace Api_TutorIdiomas.Repositories
{
    public interface ILessonRepository
    {
        Task<List<Lesson>> GetAllAsync();
        Task<Lesson?> GetByIdAsync(int id);
        Task<List<Lesson>> GetByLanguageAsync(int languageId);
        Task<List<Lesson>> GetByLevelAsync(int level);
        Task AddAsync(Lesson lesson);
        Task UpdateAsync(Lesson lesson);
        Task DeleteAsync(int id);
        Task SaveChangesAsync();
    }
}