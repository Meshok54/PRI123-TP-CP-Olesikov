using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using System.Security.Claims;

namespace Cadastral_Management.Controllers
{
    public class ApplicationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ApplicationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Application/Create - форма подачи заявления
        public IActionResult Create()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // POST: /Application/Create - обработка подачи заявления
        [HttpPost]
        public async Task<IActionResult> Create(
            string applicationType,
            string cadastralNumber,
            string address,
            decimal area,
            string objectType,
            string citizenComment,
            IFormFile documentFile)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var citizenId = int.Parse(userId);

                // Проверка типа заявления
                if (applicationType == "Обновление" && string.IsNullOrEmpty(cadastralNumber))
                {
                    ViewBag.Error = "Для заявления типа 'Обновление' необходимо указать кадастровый номер";
                    return View();
                }

                // Валидация файла (если загружен)
                if (documentFile != null && documentFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(documentFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                    {
                        ViewBag.Error = "Недопустимый формат файла. Разрешены: PDF, DOC, DOCX, JPG, JPEG, PNG";
                        return View();
                    }

                    if (documentFile.Length > 10 * 1024 * 1024) // 10 MB
                    {
                        ViewBag.Error = "Размер файла не должен превышать 10 МБ";
                        return View();
                    }
                }

                // Создание заявления
                var application = new Application
                {
                    ApplicationDate = DateTime.Now,
                    ApplicationStatus = "Принят к проверке",
                    ApplicationType = applicationType,
                    CitizenComment = citizenComment,
                    ApplicantId = citizenId,
                    CreatedAt = DateTime.Now
                };

                if (applicationType == "Регистрация")
                {
                    application.CitizenComment = $"{address}, {area} кв.м, {objectType}. " +
                                                (string.IsNullOrEmpty(citizenComment) ? "" : $"Комментарий: {citizenComment}");
                }
                else if (applicationType == "Обновление")
                {
                    var existingObject = await _context.CadastralObjects
                        .FirstOrDefaultAsync(co => co.CadastralNumber == cadastralNumber);

                    if (existingObject == null)
                    {
                        ViewBag.Error = "Объект с указанным кадастровым номером не найден";
                        return View();
                    }

                    application.CadastralObjectId = existingObject.CadastralObjectId;
                    application.CitizenComment = $"ОБНОВЛЕНИЕ: {address}, {area} кв.м, {objectType}. " +
                                                (string.IsNullOrEmpty(citizenComment) ? "" : $"Комментарий: {citizenComment}");
                }

                // Сохраняем заявление
                _context.Applications.Add(application);
                await _context.SaveChangesAsync();

                // Создаем запись в истории
                var history = new ApplicationHistory
                {
                    ApplicationId = application.ApplicationId,
                    OldStatus = null,
                    NewStatus = "Принят к проверке",
                    ChangeDate = DateTime.Now,
                    ChangedByEmployeeId = null,
                    HistoryComment = "Заявление создано автоматически системой"
                };
                _context.ApplicationHistories.Add(history);

                // Сохраняем документ (если есть)
                if (documentFile != null && documentFile.Length > 0)
                {
                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "applications");

                    // Создаем папку, если не существует
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                    }

                    // Генерируем уникальное имя файла
                    var fileName = $"{application.ApplicationId}_{Guid.NewGuid()}{Path.GetExtension(documentFile.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await documentFile.CopyToAsync(stream);
                    }

                    // Создаем запись в Attachments
                    var attachment = new Attachment
                    {
                        ApplicationId = application.ApplicationId,
                        FileName = documentFile.FileName,
                        FilePath = $"/uploads/applications/{fileName}",
                        UploadDate = DateTime.Now
                    };
                    _context.Attachments.Add(attachment);
                }

                await _context.SaveChangesAsync();

                ViewBag.Success = $"Заявление успешно подано! Номер вашего заявления: {application.ApplicationId}";
                if (documentFile != null)
                {
                    ViewBag.Success += "<br>Документ успешно прикреплен.";
                }

                // Очищаем форму
                ModelState.Clear();
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Произошла ошибка при подаче заявления: {ex.Message}";
                return View();
            }
        }

        // GET: /Application/ViewAll - посмотреть все заявления
        public IActionResult ViewAll()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // GET: /Application/MyApplications - список моих заявлений
        public async Task<IActionResult> MyApplications()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var citizenId = int.Parse(userId);

            var applications = await _context.Applications
                .Include(a => a.CadastralObject)
                .Where(a => a.ApplicantId == citizenId)
                .OrderByDescending(a => a.ApplicationDate)
                .ToListAsync();

            return View(applications);
        }
    }
}