using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly BdContext _context;
        public RefreshTokenRepository(BdContext context) => _context = context;

        public Task<RefreshToken?> GetByTokenAsync(string token) =>
            _context.RefreshTokens.Include(r => r.User)
                                  .FirstOrDefaultAsync(r => r.Token == token);

        public async Task AddAsync(RefreshToken token) =>
            await _context.RefreshTokens.AddAsync(token);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}