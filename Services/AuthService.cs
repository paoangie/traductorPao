using Api_TutorIdiomas.Models;
using Microsoft.IdentityModel.Tokens;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;
using Api_TutorIdiomas.Settings;
using Microsoft.Extensions.Options;

namespace Api_TutorIdiomas.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshAsync(string refreshToken);
        Task RevokeAsync(string refreshToken);
        Task<User?> GetUserByIdAsync(Guid id);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly TokenService _tokenService;
        private readonly JwtSettings _jwt;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepo,
            IRefreshTokenRepository refreshTokenRepo,
            TokenService tokenService,
            IOptions<JwtSettings> jwt,
            ILogger<AuthService> logger)
        {
            _userRepo = userRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _tokenService = tokenService;
            _jwt = jwt.Value;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            if (request.Password != request.ConfirmPassword)
                throw new ArgumentException("Las contraseñas no coinciden");

            var existing = await _userRepo.GetByEmailAsync(request.Email);
            if (existing != null)
                throw new InvalidOperationException("El email ya está registrado");

            var user = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "User"
            };

            await _userRepo.AddAsync(user);
            await _userRepo.SaveChangesAsync();

            _logger.LogInformation("Usuario registrado: {Email}", request.Email);

            return await IssueTokensAsync(user);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepo.GetWithTokensAsync(request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Intento de login fallido para {Email}", request.Email);
                throw new UnauthorizedAccessException("Credenciales inválidas");
            }

            _logger.LogInformation("Usuario autenticado: {Email}", request.Email);

            return await IssueTokensAsync(user);
        }

        public async Task<AuthResponse> RefreshAsync(string refreshToken)
        {
            var stored = await _refreshTokenRepo.GetByTokenAsync(refreshToken);
            if (stored == null)
            {
                _logger.LogWarning("Intento de refresh con token inexistente");
                throw new SecurityTokenException("Token de refresco inválido");
            }

            if (stored.IsRevoked)
            {
                _logger.LogWarning("Intento de refresh con token revocado para usuario {UserId}", stored.UserId);
                throw new SecurityTokenException("Token de refresco ya fue utilizado");
            }

            if (stored.Expires < DateTime.UtcNow)
            {
                _logger.LogWarning("Intento de refresh con token expirado para usuario {UserId}", stored.UserId);
                throw new SecurityTokenException("Token de refresco expirado");
            }

            stored.IsRevoked = true;
            await _refreshTokenRepo.SaveChangesAsync();

            return await IssueTokensAsync(stored.User);
        }

        public async Task RevokeAsync(string refreshToken)
        {
            var stored = await _refreshTokenRepo.GetByTokenAsync(refreshToken);
            if (stored == null)
                throw new SecurityTokenException("Token no encontrado");

            stored.IsRevoked = true;
            await _refreshTokenRepo.SaveChangesAsync();

            _logger.LogInformation("Token revocado para usuario {UserId}", stored.UserId);
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _userRepo.GetByIdAsync(id);
        }

        private async Task<AuthResponse> IssueTokensAsync(User user)
        {
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            var newRefreshToken = new RefreshToken
            {
                Token = refreshToken,
                Expires = DateTime.UtcNow.AddDays(_jwt.RefreshExpiryDays),
                UserId = user.Id,
                IsRevoked = false
            };

            await _refreshTokenRepo.AddAsync(newRefreshToken);
            await _refreshTokenRepo.SaveChangesAsync();

            return new AuthResponse(accessToken, refreshToken);
        }
    }
}
