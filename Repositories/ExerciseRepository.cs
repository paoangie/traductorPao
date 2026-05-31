using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class ExerciseRepository : IExerciseRepository
    {
        private readonly BdContext _context;
        public ExerciseRepository(BdContext context) => _context = context;

        public Task<List<Exercise>> GetByLessonAsync(int lessonId) =>
            _context.Exercises
                .Where(e => e.LessonId == lessonId)
                .Include(e => e.Lesson)
                .ThenInclude(l => l!.Language)
                .OrderBy(e => e.Id)
                .ToListAsync();

        public Task<Exercise?> GetByIdAsync(int id) =>
            _context.Exercises
                .Include(e => e.Lesson)
                .ThenInclude(l => l!.Language)
                .FirstOrDefaultAsync(e => e.Id == id);

        public async Task<Exercise?> GetNextExerciseAsync(int lessonId, int currentExerciseId)
        {
            return await _context.Exercises
                .Where(e => e.LessonId == lessonId && e.Id > currentExerciseId)
                .Include(e => e.Lesson)
                .ThenInclude(l => l!.Language)
                .OrderBy(e => e.Id)
                .FirstOrDefaultAsync();
        }
    }
}
