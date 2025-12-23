// Services/Implementation/SessionService.cs
using Microsoft.AspNetCore.Http;

namespace Cadastral_Management.Services.Implementation
{
    public class SessionService : ISessionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ISession Session => _httpContextAccessor.HttpContext?.Session;

        public string GetUserId() => Session?.GetString("UserId");
        public string GetUserName() => Session?.GetString("UserName");
        public string GetUserType() => Session?.GetString("UserType");

        public void SetUserId(int userId) => Session?.SetString("UserId", userId.ToString());
        public void SetUserName(string userName) => Session?.SetString("UserName", userName);
        public void SetUserType(string userType) => Session?.SetString("UserType", userType);
        public void SetString(string key, string value) => Session?.SetString(key, value);
        public string GetString(string key) => Session?.GetString(key);

        public int? GetInt32(string key)
        {
            var value = Session?.GetInt32(key);
            return value ?? (int.TryParse(Session?.GetString(key), out int intValue) ? intValue : (int?)null);
        }

        public void Remove(string key) => Session?.Remove(key);

        public void ClearSession() => Session?.Clear();

        public bool IsAuthenticated() => !string.IsNullOrEmpty(GetUserId());
        public bool IsAdmin() => GetUserType() == "Admin";
        public bool IsEmployee() => GetUserType() == "Employee" || IsAdmin();
        public bool IsCitizen() => GetUserType() == "Citizen";
    }
}