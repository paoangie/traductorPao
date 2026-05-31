using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class LessonRepository : ILessonRepository
    {
        private readonly BdContext _context;
        public LessonRepository(BdContext context) => _context = context;

        public Task<List<Lesson>> GetAllAsync() =>
            _context.Lessons
                .Include(l => l.Language)
                .Include(l => l.Exercises)
                .OrderBy(l => l.LanguageId)
                .ThenBy(l => l.Level)
                .ThenBy(l => l.Id)
                .ToListAsync();

        public Task<Lesson?> GetByIdAsync(int id) =>
            _context.Lessons
                .Include(l => l.Language)
                .Include(l => l.Exercises)
                .FirstOrDefaultAsync(l => l.Id == id);

        public Task<List<Lesson>> GetByLanguageAsync(int languageId) =>
            _context.Lessons
                .Where(l => l.LanguageId == languageId)
                .Include(l => l.Language)
                .Include(l => l.Exercises)
                .OrderBy(l => l.Level)
                .ThenBy(l => l.Id)
                .ToListAsync();

        public Task<List<Lesson>> GetByLevelAsync(int level) =>
            _context.Lessons
                .Where(l => l.Level == level)
                .Include(l => l.Language)
                .OrderBy(l => l.LanguageId)
                .ThenBy(l => l.Id)
                .ToListAsync();

        public async Task AddAsync(Lesson lesson) =>
            await _context.Lessons.AddAsync(lesson);

        public Task UpdateAsync(Lesson lesson)
        {
            _context.Lessons.Update(lesson);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var lesson = await GetByIdAsync(id);
            if (lesson != null)
                _context.Lessons.Remove(lesson);
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
