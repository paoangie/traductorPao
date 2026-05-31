namespace Api_TutorIdiomas.Services
{
    public interface IExerciseScoringService
    {
        Task<ExerciseScoreResult> EvaluateTranslationAsync(
            string userAnswer,
            string exerciseContent,
            string languageName,
            string lessonTitle,
            string theoryContext
        );

        Task<ExerciseScoreResult> EvaluateGrammarAsync(
            string userAnswer,
            string exerciseContent,
            string languageName,
            string lessonTitle,
            string theoryContext
        );

        ExerciseScoreResult EvaluatePronunciation(
            string userAnswer,
            string? expectedPhrase
        );

        int CalculateTranslationScore(
            string userAnswer,
            string expectedAnswer
        );

        int CalculateSimilarity(
            string input,
            string expected
        );
    }
}
