using System.ComponentModel.DataAnnotations;

namespace Api_TutorIdiomas.Models.DTOs
{
    public class RefreshRequest
    {
        [Required(ErrorMessage = "El token de refresco es requerido")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
