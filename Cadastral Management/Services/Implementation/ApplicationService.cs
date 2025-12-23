using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Cadastral_ManagementServices;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Cadastral_Management.Services.Implementation
{
    public class ApplicationService : IApplicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;

        public ApplicationService(ApplicationDbContext context, IFileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        public async Task<Application> CreateApplicationAsync(Application application, IFormFile documentFile = null)
        {
            _context.Applications.Add(application);
            await _context.SaveChangesAsync();

            // Добавляем историю
            await AddApplicationHistoryAsync(
                application.ApplicationId,
                null,
                "Принят к проверке",
                null,
                "Заявление создано автоматически системой");

            // Сохраняем документ
            if (documentFile != null && documentFile.Length > 0)
            {
                var savedPath = await _fileService.SaveApplicationDocumentAsync(documentFile, application.ApplicationId);

                var attachment = new Attachment
                {
                    ApplicationId = application.ApplicationId,
                    FileName = documentFile.FileName,
                    FilePath = savedPath,
                    UploadDate = DateTime.Now
                };
                _context.Attachments.Add(attachment);
                await _context.SaveChangesAsync();
            }

            return application;
        }

        public async Task<Application> ApproveApplicationAsync(int applicationId, int employeeId, string comment)
        {
            var application = await _context.Applications
                .Include(a => a.Applicant)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null) return null;

            var oldStatus = application.ApplicationStatus;
            application.ApplicationStatus = "Одобрен";
            application.DecisionComment = comment;
            application.AssignedEmployeeId = employeeId;

            // История
            await AddApplicationHistoryAsync(
                applicationId,
                oldStatus,
                "Одобрен",
                employeeId,
                $"Заявление одобрено. Комментарий: {comment}");

            // Создание/обновление объекта
            if (application.ApplicationType == "Регистрация")
            {
                await CreateCadastralObjectFromApplicationAsync(application, employeeId);
            }
            else if (application.ApplicationType == "Обновление" && application.CadastralObjectId.HasValue)
            {
                await UpdateCadastralObjectFromApplicationAsync(application, employeeId);
            }

            await _context.SaveChangesAsync();
            return application;
        }

        public async Task<Application> RejectApplicationAsync(int applicationId, int employeeId, string comment)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null) return null;

            var oldStatus = application.ApplicationStatus;
            application.ApplicationStatus = "Отклонен";
            application.DecisionComment = comment;
            application.AssignedEmployeeId = employeeId;

            await AddApplicationHistoryAsync(
                applicationId,
                oldStatus,
                "Отклонен",
                employeeId,
                $"Заявление отклонено. Причина: {comment}");

            await _context.SaveChangesAsync();
            return application;
        }

        public async Task<bool> IsCadastralNumberUniqueAsync(string cadastralNumber)
        {
            return !await _context.CadastralObjects.AnyAsync(co => co.CadastralNumber == cadastralNumber);
        }

        public async Task<bool> CanUserUpdateObjectAsync(int userId, string cadastralNumber)
        {
            var obj = await _context.CadastralObjects
                .FirstOrDefaultAsync(co => co.CadastralNumber == cadastralNumber);

            return obj != null && obj.OwnerId == userId;
        }

        public string GenerateUniqueCadastralNumber()
        {
            var random = new Random();
            string cadastralNumber;
            bool isUnique;

            do
            {
                var part1 = random.Next(10, 100).ToString("00");
                var part2 = random.Next(10, 100).ToString("00");
                var part3 = random.Next(100, 1000).ToString("000");
                var part4 = random.Next(1000, 10000).ToString("0000");

                cadastralNumber = $"{part1}:{part2}:{part3}:{part4}";
                isUnique = !_context.CadastralObjects.Any(co => co.CadastralNumber == cadastralNumber);
            } while (!isUnique);

            return cadastralNumber;
        }

        public async Task AddApplicationHistoryAsync(int applicationId, string oldStatus, string newStatus,
            int? employeeId = null, string comment = "")
        {
            var history = new ApplicationHistory
            {
                ApplicationId = applicationId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangeDate = DateTime.Now,
                ChangedByEmployeeId = employeeId,
                HistoryComment = comment
            };
            _context.ApplicationHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        private async Task CreateCadastralObjectFromApplicationAsync(Application application, int employeeId)
        {
            var newObject = new CadastralObject
            {
                CadastralNumber = GenerateUniqueCadastralNumber(),
                Address = application.Address,
                Area = application.Area,
                CadastralObjectType = application.CadastralObjectType,
                OwnerId = application.ApplicantId,
                RegistrationDate = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            _context.CadastralObjects.Add(newObject);
            await _context.SaveChangesAsync();

            application.CadastralObjectId = newObject.CadastralObjectId;

            // История объекта
            var objectHistory = new CadastralObjectHistory
            {
                CadastralObjectId = newObject.CadastralObjectId,
                ChangedField = "Создание",
                OldValue = null,
                NewValue = "Объект создан на основе заявления",
                ChangeDate = DateTime.Now,
                ChangedByEmployeeId = employeeId
            };
            _context.CadastralObjectHistories.Add(objectHistory);
        }

        private async Task UpdateCadastralObjectFromApplicationAsync(Application application, int employeeId)
        {
            var obj = await _context.CadastralObjects
                .FirstOrDefaultAsync(co => co.CadastralObjectId == application.CadastralObjectId);

            if (obj == null) return;

            // Сохраняем старые значения и обновляем
            var changes = new Dictionary<string, (string Old, string New)>
            {
                ["Адрес"] = (obj.Address, application.Address),
                ["Площадь"] = (obj.Area.ToString("N2"), application.Area.ToString("N2")),
                ["Тип объекта"] = (obj.CadastralObjectType, application.CadastralObjectType)
            };

            obj.Address = application.Address;
            obj.Area = application.Area;
            obj.CadastralObjectType = application.CadastralObjectType;

            // Создаем истории изменений
            foreach (var change in changes.Where(c => c.Value.Old != c.Value.New))
            {
                var history = new CadastralObjectHistory
                {
                    CadastralObjectId = obj.CadastralObjectId,
                    ChangedField = change.Key,
                    OldValue = change.Value.Old,
                    NewValue = change.Value.New,
                    ChangeDate = DateTime.Now,
                    ChangedByEmployeeId = employeeId
                };
                _context.CadastralObjectHistories.Add(history);
            }
        }
    }
}