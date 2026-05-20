using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Data
{
    public class BdContext : DbContext
    {
        public BdContext(DbContextOptions options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Language> Languages => Set<Language>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Exercise> Exercises => Set<Exercise>();
        public DbSet<UserProgress> UserProgress => Set<UserProgress>();
        public DbSet<PronunciationAttempt> PronunciationAttempts => Set<PronunciationAttempt>();
        public DbSet<UserMistake> UserMistakes => Set<UserMistake>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Usuario: Email único
            builder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Relación User -> RefreshTokens
            builder.Entity<User>()
                .HasMany(u => u.RefreshTokens)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación Lesson -> Exercises
            builder.Entity<Lesson>()
                .HasMany(l => l.Exercises)
                .WithOne(e => e.Lesson)
                .HasForeignKey(e => e.LessonId);

            // Valores por defecto
            builder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}