using Api_TutorIdiomas.Models;

namespace Api_TutorIdiomas.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetWithTokensAsync(string email);
        Task AddAsync(User user);
        Task SaveChangesAsync();
    }
}