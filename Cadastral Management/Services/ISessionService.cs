namespace Cadastral_Management.Services
{
    public interface ISessionService
    {
        void SetUserId(int userId);
        void SetUserName(string userName);
        void SetUserType(string userType);
        string GetUserId();
        string GetUserName();
        string GetUserType();
        void ClearSession();
        bool IsAdmin();
        bool IsEmployee();
        bool IsCitizen();
    }
}