using Api_TutorIdiomas.Models;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class ProgressService
    {
        private const int MinimumScoreToComplete = 70;

        private readonly IProgressRepository _progressRepo;
        private readonly ILessonRepository _lessonRepo;

        public ProgressService(
            IProgressRepository progressRepo,
            ILessonRepository lessonRepo
        )
        {
            _progressRepo = progressRepo;
            _lessonRepo = lessonRepo;
        }

        public async Task<UserProgress> CompleteLessonAsync(
            Guid userId,
            int lessonId,
            int score
        )
        {
            var normalizedScore = NormalizeFinalScore(score);

            var lesson = await _lessonRepo.GetByIdAsync(lessonId);

            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            var progress = await _progressRepo.GetByUserAndLessonAsync(
                userId,
                lessonId
            );

            if (progress == null)
            {
                progress = new UserProgress
                {
                    UserId = userId,
                    LanguageId = lesson.LanguageId,
                    LessonId = lessonId,
                    Score = normalizedScore,
                    Completed = normalizedScore >= MinimumScoreToComplete,
                    CompletedAt = normalizedScore >= MinimumScoreToComplete
                        ? DateTime.UtcNow
                        : null
                };

                await _progressRepo.AddAsync(progress);
            }
            else
            {
                progress.Score = Math.Max(progress.Score, normalizedScore);

                if (!progress.Completed &&
                    normalizedScore >= MinimumScoreToComplete)
                {
                    progress.Completed = true;
                    progress.CompletedAt = DateTime.UtcNow;
                }

                await _progressRepo.UpdateAsync(progress);
            }

            await _progressRepo.SaveChangesAsync();

            return progress;
        }

        public async Task<int> GetUserLevelByLanguageAsync(
            Guid userId,
            int languageId
        )
        {
            var progresses = await _progressRepo.GetByUserAndLanguageAsync(
                userId,
                languageId
            );

            var completedCount = progresses.Count(progress => progress.Completed);

            if (completedCount >= 20) return 3;
            if (completedCount >= 10) return 2;

            return 1;
        }

        public static int CalculateXpEarned(int finalScore)
        {
            var score = NormalizeFinalScore(finalScore);

            if (score == 100) return 50;
            if (score >= 90) return 30;
            if (score >= 80) return 20;
            if (score >= 70) return 10;

            return 0;
        }

        public static string BuildCompletionMessage(int finalScore)
        {
            var score = NormalizeFinalScore(finalScore);

            if (score == 100)
                return "Excelente, completaste la lección sin errores.";

            if (score >= 90)
                return "Muy bien, corregiste tus errores y completaste la lección.";

            if (score >= 70)
                return "Buen avance, pero conviene repasar la teoría.";

            return "Necesitas practicar nuevamente esta lección.";
        }

        public static int CalculateFinalScore(
            int correctOnFirstAttempt,
            int correctedAfterRetry
        )
        {
            var score =
                correctOnFirstAttempt * 20 +
                correctedAfterRetry * 10;

            return NormalizeFinalScore(score);
        }

        private static int NormalizeFinalScore(int score)
        {
            return Math.Clamp(score, 0, 100);
        }
    }
}