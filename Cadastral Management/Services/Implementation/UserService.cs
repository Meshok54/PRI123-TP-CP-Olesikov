using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Microsoft.EntityFrameworkCore;

namespace Cadastral_Management.Services.Implementation
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User> AuthenticateAsync(string login, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Login == login);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            return user;
        }

        public async Task<User> CreateUserAsync(User user, string password)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> UserExistsByLoginAsync(string login)
        {
            return await _context.Users.AnyAsync(u => u.Login == login);
        }

        public async Task<bool> UserExistsByEmailAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task<bool> CitizenExistsByPassportAsync(string passportData)
        {
            return await _context.Citizens.AnyAsync(c => c.PassportData == passportData);
        }
    }
}