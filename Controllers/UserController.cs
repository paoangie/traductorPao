using Api_TutorIdiomas.Repositories;
using Api_TutorIdiomas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepo;
        private readonly TokenService _tokenService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserRepository userRepo, TokenService tokenService, ILogger<UserController> logger)
        {
            _userRepo = userRepo;
            _tokenService = tokenService;
            _logger = logger;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var user = await _userRepo.GetByIdAsync(userId.Value);
                if (user == null)
                    return NotFound(new { error = "Usuario no encontrado" });

                return Ok(new
                {
                    user.Id,
                    user.Email,
                    user.Role,
                    user.CreatedAt
                });
            }
            catch (FormatException)
            {
                return Unauthorized(new { error = "Token inválido" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener perfil del usuario");
                throw;
            }
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var user = await _userRepo.GetByIdAsync(userId.Value);
                if (user == null)
                    return NotFound(new { error = "Usuario no encontrado" });

                if (!string.IsNullOrWhiteSpace(request.Name))
                    user.Name = request.Name;

                await _userRepo.SaveChangesAsync();

                return Ok(new { message = "Perfil actualizado correctamente" });
            }
            catch (FormatException)
            {
                return Unauthorized(new { error = "Token inválido" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar perfil");
                throw;
            }
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                    string.IsNullOrWhiteSpace(request.NewPassword) ||
                    string.IsNullOrWhiteSpace(request.ConfirmPassword))
                    return BadRequest(new { error = "Todos los campos de contraseña son requeridos" });

                var user = await _userRepo.GetByIdAsync(userId.Value);
                if (user == null)
                    return NotFound(new { error = "Usuario no encontrado" });

                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                    return BadRequest(new { error = "Contraseña actual incorrecta" });

                if (request.NewPassword != request.ConfirmPassword)
                    return BadRequest(new { error = "Las contraseñas nuevas no coinciden" });

                if (request.NewPassword.Length < 6)
                    return BadRequest(new { error = "La nueva contraseña debe tener al menos 6 caracteres" });

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _userRepo.SaveChangesAsync();

                return Ok(new { message = "Contraseña actualizada correctamente" });
            }
            catch (FormatException)
            {
                return Unauthorized(new { error = "Token inválido" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar contraseña");
                throw;
            }
        }

        private Guid? GetUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return null;
            return Guid.Parse(userIdStr);
        }
    }

    public class UpdateProfileRequest
    {
        public string? Name { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
