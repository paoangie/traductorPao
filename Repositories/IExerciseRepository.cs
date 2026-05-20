using Api_TutorIdiomas.Models;

namespace Api_TutorIdiomas.Repositories
{
    public interface IExerciseRepository
    {
        Task<List<Exercise>> GetByLessonAsync(int lessonId);
        Task<Exercise?> GetByIdAsync(int id);
        Task<Exercise?> GetNextExerciseAsync(int lessonId, int currentExerciseId);
    }
}