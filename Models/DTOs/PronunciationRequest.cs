using System.ComponentModel.DataAnnotations;

namespace Api_TutorIdiomas.Models.DTOs
{
    public class PronunciationRequest
    {
        [Required(ErrorMessage = "El audio es requerido")]
        public string AudioBase64 { get; set; } = string.Empty;

        [Required(ErrorMessage = "La frase esperada es requerida")]
        [MinLength(1, ErrorMessage = "La frase esperada no puede estar vacía")]
        public string ExpectedPhrase { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "El ID del ejercicio debe ser un número positivo")]
        public int ExerciseId { get; set; }
    }
}
