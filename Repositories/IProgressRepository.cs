using Api_TutorIdiomas.Models;

namespace Api_TutorIdiomas.Repositories
{
    public interface IProgressRepository
    {
        // Métodos existentes
        Task<UserProgress?> GetByUserAndLessonAsync(Guid userId, int lessonId);
        Task<List<UserProgress>> GetByUserAsync(Guid userId);
        Task<List<UserProgress>> GetByUserAndLanguageAsync(Guid userId, int languageId);
        Task AddAsync(UserProgress progress);
        Task UpdateAsync(UserProgress progress);
        Task SaveChangesAsync();
        Task<int> GetTotalXpByUserAsync(Guid userId);
        Task<double> GetAverageScoreByUserAsync(Guid userId);

        // ✅ NUEVOS MÉTODOS
        Task<UserProgress> UpdateLessonProgressAsync(Guid userId, int lessonId, int score, bool completed);
        Task UpdateExerciseScoreAsync(Guid userId, int exerciseId, int score);
        Task<Lesson?> GetNextLessonAsync(Guid userId, int currentLessonId);
        Task<List<UserLeaderboardDto>> GetLeaderboardAsync(int limit);
    }

    // DTO para leaderboard
    public class UserLeaderboardDto
    {
        public string Email { get; set; } = string.Empty;
        public int TotalXp { get; set; }
        public int CompletedLessons { get; set; }
        public int Rank { get; set; }
    }
}