using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class ProgressRepository : IProgressRepository
    {
        private readonly BdContext _context;

        public ProgressRepository(BdContext context)
        {
            _context = context;
        }

        public Task<UserProgress?> GetByUserAndLessonAsync(Guid userId, int lessonId) =>
            _context.UserProgress.FirstOrDefaultAsync(up => up.UserId == userId && up.LessonId == lessonId);

        public Task<List<UserProgress>> GetByUserAsync(Guid userId) =>
            _context.UserProgress.Where(up => up.UserId == userId)
                                 .Include(up => up.Lesson)
                                 .Include(up => up.Language)
                                 .ToListAsync();

        public Task<List<UserProgress>> GetByUserAndLanguageAsync(Guid userId, int languageId) =>
            _context.UserProgress.Where(up => up.UserId == userId && up.LanguageId == languageId)
                                 .Include(up => up.Lesson)
                                 .ToListAsync();

        public async Task AddAsync(UserProgress progress) =>
            await _context.UserProgress.AddAsync(progress);

        public Task UpdateAsync(UserProgress progress)
        {
            _context.UserProgress.Update(progress);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<int> GetTotalXpByUserAsync(Guid userId)
        {
            var completedLessons = await _context.UserProgress
                .Where(up => up.UserId == userId && up.Completed)
                .Include(up => up.Lesson)
                .ToListAsync();
            return completedLessons.Sum(up => up.Lesson?.XpReward ?? 0);
        }

        public async Task<double> GetAverageScoreByUserAsync(Guid userId)
        {
            var progresses = await _context.UserProgress
                .Where(up => up.UserId == userId && up.Score > 0)
                .ToListAsync();
            if (!progresses.Any()) return 0;
            return progresses.Average(p => p.Score);
        }

        // ✅ IMPLEMENTACIÓN DE NUEVOS MÉTODOS
        public async Task<UserProgress> UpdateLessonProgressAsync(Guid userId, int lessonId, int score, bool completed)
        {
            var progress = await GetByUserAndLessonAsync(userId, lessonId);
            var lesson = await _context.Lessons.FindAsync(lessonId);

            if (progress == null)
            {
                progress = new UserProgress
                {
                    UserId = userId,
                    LessonId = lessonId,
                    LanguageId = lesson?.LanguageId ?? 1,
                    Score = score,
                    Completed = completed,
                    CompletedAt = completed ? DateTime.UtcNow : null
                };
                await AddAsync(progress);
            }
            else
            {
                progress.Score = Math.Max(progress.Score, score);
                if (!progress.Completed && completed)
                {
                    progress.Completed = true;
                    progress.CompletedAt = DateTime.UtcNow;
                }
                await UpdateAsync(progress);
            }

            await SaveChangesAsync();
            return progress;
        }

        public async Task UpdateExerciseScoreAsync(Guid userId, int exerciseId, int score)
        {
            // Buscar el progreso de la lección que contiene este ejercicio
            var exercise = await _context.Exercises.FindAsync(exerciseId);
            if (exercise == null) return;

            var progress = await GetByUserAndLessonAsync(userId, exercise.LessonId);
            if (progress != null)
            {
                // Actualizar score promedio o guardar en una tabla separada
                // Por simplicidad, actualizamos el score de la lección
                progress.Score = (progress.Score + score) / 2;
                await UpdateAsync(progress);
                await SaveChangesAsync();
            }
        }

        public async Task<Lesson?> GetNextLessonAsync(Guid userId, int currentLessonId)
        {
            var currentLesson = await _context.Lessons.FindAsync(currentLessonId);
            if (currentLesson == null) return null;

            // Obtener siguiente lección del mismo idioma y nivel
            var nextLesson = await _context.Lessons
                .Where(l => l.LanguageId == currentLesson.LanguageId && l.Id > currentLessonId)
                .OrderBy(l => l.Id)
                .FirstOrDefaultAsync();

            return nextLesson;
        }

        public async Task<List<UserLeaderboardDto>> GetLeaderboardAsync(int limit)
        {
            var users = await _context.Users
                .Select(u => new UserLeaderboardDto
                {
                    Email = u.Email,
                    TotalXp = _context.UserProgress
                        .Where(up => up.UserId == u.Id && up.Completed)
                        .Sum(up => up.Lesson != null ? up.Lesson.XpReward : 0),
                    CompletedLessons = _context.UserProgress
                        .Count(up => up.UserId == u.Id && up.Completed)
                })
                .OrderByDescending(u => u.TotalXp)
                .Take(limit)
                .ToListAsync();

            // Asignar rankings
            for (int i = 0; i < users.Count; i++)
            {
                users[i].Rank = i + 1;
            }

            return users;
        }
    }
}