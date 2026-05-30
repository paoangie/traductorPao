namespace Api_TutorIdiomas.Services
{
    public interface IExerciseScoringService
    {
        ExerciseScoreResult EvaluateTranslation(
            string userAnswer,
            string exerciseContent
        );

        ExerciseScoreResult EvaluateGrammar(
            string userAnswer,
            string exerciseContent
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