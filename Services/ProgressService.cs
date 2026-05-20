using Api_TutorIdiomas.Models;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class ProgressService
    {
        private readonly IProgressRepository _progressRepo;
        private readonly ILessonRepository _lessonRepo;

        public ProgressService(IProgressRepository progressRepo, ILessonRepository lessonRepo)
        {
            _progressRepo = progressRepo;
            _lessonRepo = lessonRepo;
        }

        public async Task<UserProgress> CompleteLessonAsync(Guid userId, int lessonId, int score)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            var progress = await _progressRepo.GetByUserAndLessonAsync(userId, lessonId);

            if (progress == null)
            {
                progress = new UserProgress
                {
                    UserId = userId,
                    LanguageId = lesson.LanguageId,
                    LessonId = lessonId,
                    Score = score,
                    Completed = score >= 70, // Se considera completada si tiene más de 70%
                    CompletedAt = score >= 70 ? DateTime.UtcNow : null
                };
                await _progressRepo.AddAsync(progress);
            }
            else
            {
                progress.Score = Math.Max(progress.Score, score);
                if (!progress.Completed && score >= 70)
                {
                    progress.Completed = true;
                    progress.CompletedAt = DateTime.UtcNow;
                }
                await _progressRepo.UpdateAsync(progress);
            }

            await _progressRepo.SaveChangesAsync();
            return progress;
        }

        public async Task<int> GetUserLevelByLanguageAsync(Guid userId, int languageId)
        {
            var progresses = await _progressRepo.GetByUserAndLanguageAsync(userId, languageId);
            var completedCount = progresses.Count(p => p.Completed);

            if (completedCount >= 20) return 3; // Avanzado
            if (completedCount >= 10) return 2; // Intermedio
            return 1; // Básico
        }
    }
}