using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class PronunciationRepository : IPronunciationRepository
    {
        private readonly BdContext _context;
        public PronunciationRepository(BdContext context) => _context = context;

        public Task<PronunciationAttempt?> GetByIdAsync(int id) =>
            _context.PronunciationAttempts.FirstOrDefaultAsync(p => p.Id == id);

        public Task<List<PronunciationAttempt>> GetByUserAsync(Guid userId) =>
            _context.PronunciationAttempts.Where(p => p.UserId == userId)
                                          .OrderByDescending(p => p.CreatedAt)
                                          .ToListAsync();

        public Task<List<PronunciationAttempt>> GetByExerciseAsync(int exerciseId) =>
            _context.PronunciationAttempts.Where(p => p.ExerciseId == exerciseId).ToListAsync();

        public async Task AddAttemptAsync(PronunciationAttempt attempt) =>
            await _context.PronunciationAttempts.AddAsync(attempt);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<double> GetAverageScoreByUserAsync(Guid userId)
        {
            var attempts = await GetByUserAsync(userId);
            if (!attempts.Any()) return 0;
            return attempts.Average(a => a.Score);
        }
    }
}