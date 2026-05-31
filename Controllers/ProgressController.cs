using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Repositories;
using Api_TutorIdiomas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProgressController : ControllerBase
    {
        private readonly IProgressRepository _progressRepo;
        private readonly IUserRepository _userRepo;
        private readonly ProgressService _progressService;
        private readonly BdContext _context;
        private readonly ILogger<ProgressController> _logger;

        public ProgressController(
            IProgressRepository progressRepo,
            IUserRepository userRepo,
            ProgressService progressService,
            BdContext context,
            ILogger<ProgressController> logger)
        {
            _progressRepo = progressRepo;
            _userRepo = userRepo;
            _progressService = progressService;
            _context = context;
            _logger = logger;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProgress()
        {
            try
            {
                var userId = GetUserId();

                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var progress = await _progressRepo.GetByUserAsync(userId.Value);

                var totalLessons = progress.Count;
                var completedLessons = progress.Count(p => p.Completed);
                var totalScore = progress.Sum(p => p.Score);
                var totalXp = await _progressRepo.GetTotalXpByUserAsync(userId.Value);

                var streak = await CalculateStreak(userId.Value);
                var level = (totalXp / 500) + 1;
                var xpToNextLevel = 500 - (totalXp % 500);

                return Ok(new
                {
                    level,
                    totalXp,
                    xpToNextLevel,
                    streak,
                    totalLessons,
                    completedLessons,
                    completionPercentage = totalLessons > 0
                        ? completedLessons * 100 / totalLessons
                        : 0,
                    averageScore = totalLessons > 0
                        ? totalScore / totalLessons
                        : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener progreso del usuario");
                throw;
            }
        }

        [HttpGet("me/language/{languageId}")]
        public async Task<IActionResult> GetProgressByLanguage(int languageId)
        {
            try
            {
                var userId = GetUserId();

                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var progress = await _progressRepo.GetByUserAndLanguageAsync(
                    userId.Value,
                    languageId
                );

                var currentLevel = await GetUserLevel(userId.Value, languageId);

                return Ok(new
                {
                    languageId,
                    totalLessons = progress.Count,
                    completedLessons = progress.Count(p => p.Completed),
                    currentLevel,
                    progress = progress.Select(p => new
                    {
                        p.LessonId,
                        p.Lesson?.Title,
                        p.Score,
                        p.Completed,
                        p.CompletedAt
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al obtener progreso por idioma {LanguageId}",
                    languageId
                );

                throw;
            }
        }

        [HttpPost("lesson/{lessonId}/complete")]
        public async Task<IActionResult> CompleteLesson(
            int lessonId,
            [FromBody] CompleteLessonRequest request
        )
        {
            try
            {
                var userId = GetUserId();

                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var lesson = await _context.Lessons.FindAsync(lessonId);

                if (lesson == null)
                    return NotFound(new { error = "Lección no encontrada" });

                var existingProgress = await _progressRepo.GetByUserAndLessonAsync(
                    userId.Value,
                    lessonId
                );

                var wasAlreadyCompleted = existingProgress?.Completed == true;

                var finalScore = ResolveFinalScore(request);

                var result = await _progressService.CompleteLessonAsync(
                    userId.Value,
                    lessonId,
                    finalScore
                );

                var xpEarned = result.Completed && !wasAlreadyCompleted
                    ? ProgressService.CalculateXpEarned(result.Score)
                    : 0;

                var nextLesson = await _progressRepo.GetNextLessonAsync(
                    userId.Value,
                    lessonId
                );

                return Ok(new
                {
                    completed = result.Completed,
                    score = result.Score,
                    xpEarned,
                    nextLessonUnlocked = nextLesson != null,
                    nextLessonId = nextLesson?.Id,
                    nextLessonTitle = nextLesson?.Title,
                    message = result.Completed
                        ? $"{ProgressService.BuildCompletionMessage(result.Score)} +{xpEarned} XP"
                        : ProgressService.BuildCompletionMessage(result.Score),
                    correctOnFirstAttempt = request.CorrectOnFirstAttempt,
                    correctedAfterRetry = request.CorrectedAfterRetry,
                    totalPracticeExercises = request.TotalExercises
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al completar lección {LessonId}", lessonId);
                throw;
            }
        }

        [HttpGet("streak")]
        public async Task<IActionResult> GetStreak()
        {
            try
            {
                var userId = GetUserId();

                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var streak = await CalculateStreak(userId.Value);

                return Ok(new
                {
                    currentStreak = streak,
                    message = streak >= 7
                        ? "¡Racha increíble! Sigue así"
                        : streak >= 3
                            ? "¡Bien! Mantén tu racha"
                            : "Practica cada día para aumentar tu racha"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener racha");
                throw;
            }
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 10)
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                if (role != "Admin")
                    return Forbid();

                if (limit < 1 || limit > 100)
                    limit = 10;

                var leaderboard = await _progressRepo.GetLeaderboardAsync(limit);

                return Ok(leaderboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener leaderboard");
                throw;
            }
        }

        [HttpGet("me/rank")]
        public async Task<IActionResult> GetMyRank()
        {
            try
            {
                var userId = GetUserId();

                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var userXp = await _progressRepo.GetTotalXpByUserAsync(userId.Value);

                var userCompleted = await _context.UserProgress
                    .CountAsync(up => up.UserId == userId.Value && up.Completed);

                var totalUsers = await _context.Users.CountAsync();

                var usersAbove = await _context.Users
                    .Select(u => new
                    {
                        u.Id,
                        TotalXp = _context.UserProgress
                            .Where(up => up.UserId == u.Id && up.Completed)
                            .Sum(up => up.Lesson != null ? up.Lesson.XpReward : 0)
                    })
                    .Where(u => u.TotalXp > userXp)
                    .CountAsync();

                var rank = usersAbove + 1;

                return Ok(new
                {
                    email = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
                    totalXp = userXp,
                    completedLessons = userCompleted,
                    rank,
                    totalUsers,
                    percentile = totalUsers > 0
                        ? Math.Round((double)(totalUsers - rank) / totalUsers * 100, 1)
                        : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rank del usuario");
                throw;
            }
        }

        private static int ResolveFinalScore(CompleteLessonRequest request)
        {
            if (request.TotalExercises > 0 &&
                (request.CorrectOnFirstAttempt > 0 || request.CorrectedAfterRetry > 0))
            {
                return ProgressService.CalculateFinalScore(
                    request.CorrectOnFirstAttempt,
                    request.CorrectedAfterRetry
                );
            }

            return Math.Clamp(request.Score, 0, 100);
        }

        private Guid? GetUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdStr))
                return null;

            try
            {
                return Guid.Parse(userIdStr);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private async Task<int> CalculateStreak(Guid userId)
        {
            var progress = await _progressRepo.GetByUserAsync(userId);

            var completedDates = progress
                .Where(p => p.Completed && p.CompletedAt.HasValue)
                .Select(p => p.CompletedAt!.Value.Date)
                .Distinct()
                .OrderByDescending(date => date)
                .ToList();

            if (!completedDates.Any())
                return 0;

            var streak = 1;
            var currentDate = completedDates.First();

            for (var index = 1; index < completedDates.Count; index++)
            {
                var expectedDate = currentDate.AddDays(-1);

                if (completedDates[index] == expectedDate)
                {
                    streak++;
                    currentDate = expectedDate;
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        private async Task<int> GetUserLevel(Guid userId, int languageId)
        {
            var progress = await _progressRepo.GetByUserAndLanguageAsync(
                userId,
                languageId
            );

            var completedCount = progress.Count(p => p.Completed);

            if (completedCount >= 20) return 3;
            if (completedCount >= 10) return 2;

            return 1;
        }
    }

    public class CompleteLessonRequest
    {
        public int Score { get; set; }

        public bool Completed { get; set; }

        public int TimeSpentSeconds { get; set; }

        public int TotalExercises { get; set; } = 5;

        public int CorrectOnFirstAttempt { get; set; }

        public int CorrectedAfterRetry { get; set; }
    }
}