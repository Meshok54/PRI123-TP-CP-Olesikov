// Services/IApplicationService.cs
using Cadastral_Management.Models;

namespace Cadastral_Management.Services
{
    public interface IApplicationService
    {
        // Создание заявлений
        Task<Application> CreateApplicationAsync(Application application, IFormFile documentFile = null);

        // Обработка заявлений
        Task<Application> ApproveApplicationAsync(int applicationId, int employeeId, string comment);
        Task<Application> RejectApplicationAsync(int applicationId, int employeeId, string comment);

        // Поиск и валидация
        Task<bool> IsCadastralNumberUniqueAsync(string cadastralNumber);
        Task<bool> CanUserUpdateObjectAsync(int userId, string cadastralNumber);

        // Генерация данных
        string GenerateUniqueCadastralNumber();

        // История
        Task AddApplicationHistoryAsync(int applicationId, string oldStatus, string newStatus,
            int? employeeId = null, string comment = "");
    }
}