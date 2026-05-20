using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly BdContext _context;
        public UserRepository(BdContext context) => _context = context;

        public Task<User?> GetByEmailAsync(string email) =>
            _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public Task<User?> GetByIdAsync(Guid id) =>
            _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        public Task<User?> GetWithTokensAsync(string email) =>
            _context.Users.Include(u => u.RefreshTokens)
                          .FirstOrDefaultAsync(u => u.Email == email);

        public async Task AddAsync(User user) =>
            await _context.Users.AddAsync(user);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}