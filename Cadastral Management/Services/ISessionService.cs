// Services/ISessionService.cs
namespace Cadastral_Management.Services
{
    public interface ISessionService
    {
        // Основные методы
        string GetUserId();
        string GetUserName();
        string GetUserType();
        bool IsAuthenticated();
        bool IsEmployee();
        bool IsAdmin();
        bool IsCitizen();

        // Set методы
        void SetUserId(int userId);
        void SetUserName(string userName);
        void SetUserType(string userType);
        void SetString(string key, string value);

        // Get методы
        string GetString(string key);
        int? GetInt32(string key);

        // Другие методы
        void ClearSession();
    }
}