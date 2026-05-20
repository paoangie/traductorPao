using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Repositories
{
    public class LanguageRepository : ILanguageRepository
    {
        private readonly BdContext _context;
        public LanguageRepository(BdContext context) => _context = context;

        public Task<List<Language>> GetAllAsync() =>
            _context.Languages.ToListAsync();

        public Task<Language?> GetByIdAsync(int id) =>
            _context.Languages.FirstOrDefaultAsync(l => l.Id == id);

        public Task<Language?> GetByCodeAsync(string code) =>
            _context.Languages.FirstOrDefaultAsync(l => l.Code == code);
    }
}