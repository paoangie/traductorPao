using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class MistakeRepository : IMistakeRepository
    {
        private readonly BdContext _context;

        public MistakeRepository(BdContext context)
        {
            _context = context;
        }

        public Task<List<UserMistake>> GetByUserAndLanguageAsync(Guid userId, int languageId) =>
            _context.UserMistakes
                .Where(m => m.UserId == userId && m.LanguageId == languageId)
                .ToListAsync();

        public Task<List<UserMistake>> GetCommonMistakesAsync(Guid userId, int languageId, int limit = 5) =>
            _context.UserMistakes
                .Where(m => m.UserId == userId && m.LanguageId == languageId)
                .OrderByDescending(m => m.Count)
                .Take(limit)
                .ToListAsync();

        public async Task AddOrUpdateAsync(UserMistake mistake)
        {
            var existing = await _context.UserMistakes
                .FirstOrDefaultAsync(m =>
                    m.UserId == mistake.UserId &&
                    m.LanguageId == mistake.LanguageId &&
                    m.MistakeText.ToLower() == mistake.MistakeText.ToLower() &&
                    m.ExerciseType == mistake.ExerciseType);

            if (existing != null)
            {
                existing.Count++;
                existing.LastOccurrence = DateTime.UtcNow;
                _context.UserMistakes.Update(existing);
            }
            else
            {
                await _context.UserMistakes.AddAsync(mistake);
            }
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
