using Cadastral_Management.Data;
using Cadastral_Management.Models;

namespace Cadastral_Management.Services
{
    public interface IUserService
    {
        Task<User> AuthenticateAsync(string login, string password);
        Task<User> CreateUserAsync(User user, string password);
        Task<bool> UserExistsByLoginAsync(string login);
        Task<bool> UserExistsByEmailAsync(string email);
        Task<bool> CitizenExistsByPassportAsync(string passportData);
    }
}