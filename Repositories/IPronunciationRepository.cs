using Api_TutorIdiomas.Models;

namespace Api_TutorIdiomas.Repositories
{
    public interface IPronunciationRepository
    {
        Task<PronunciationAttempt?> GetByIdAsync(int id);
        Task<List<PronunciationAttempt>> GetByUserAsync(Guid userId);
        Task<List<PronunciationAttempt>> GetByExerciseAsync(int exerciseId);
        Task AddAttemptAsync(PronunciationAttempt attempt);
        Task SaveChangesAsync();
        Task<double> GetAverageScoreByUserAsync(Guid userId);
    }
}