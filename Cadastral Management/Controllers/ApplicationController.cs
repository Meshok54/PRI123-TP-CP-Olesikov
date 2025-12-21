using System.Globalization;
using System.Security.Claims;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

                var application = new Application
                {
                    ApplicationDate = DateTime.Now,
                    ApplicationStatus = "Принят к проверке",
                    ApplicationType = applicationType,
                    Address = address,
                    Area = area,
                    CadastralObjectType = objectType,
                    CitizenComment = citizenComment,
                    ApplicantId = citizenId
                };

                // Обработка для "Обновление" - находим существующий объект
                if (applicationType == "Обновление")
                {
                    var existingObject = await _context.CadastralObjects
                        .FirstOrDefaultAsync(co => co.CadastralNumber == cadastralNumber);

                    if (existingObject == null)
                    {
                        ViewBag.Error = "Объект с указанным кадастровым номером не найден";
                        return View();
                    }

                    // Проверяем, что заявитель является владельцем объекта
                    if (existingObject.OwnerId != citizenId)
                    {
                        ViewBag.Error = "Вы не являетесь владельцем данного кадастрового объекта";
                        return View();
                    }

                    application.CadastralObjectId = existingObject.CadastralObjectId;
                }
                // Для "Регистрации" поле CadastralObjectId остается null

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

        // GET: /Application/ViewAll - посмотреть все заявления (для работников)
        public async Task<IActionResult> ViewAll(string status = "all", string type = "all", string search = "")
        {
            if (!IsEmployeeOrAdmin())
                return RedirectToAction("AccessDenied", "Home");

            IQueryable<Application> query = _context.Applications
                .Include(a => a.CadastralObject)
                .Include(a => a.Applicant)
                    .ThenInclude(c => c.User)
                .Include(a => a.AssignedEmployee)
                    .ThenInclude(e => e.User);

            // Фильтрация по статусу
            if (status != "all" && !string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.ApplicationStatus == status);
            }

            // Фильтрация по типу
            if (type != "all" && !string.IsNullOrEmpty(type))
            {
                query = query.Where(a => a.ApplicationType == type);
            }

            // Поиск по различным полям
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(a =>
                    a.ApplicationId.ToString().Contains(search) ||
                    (a.Applicant.User.FullName != null && a.Applicant.User.FullName.ToLower().Contains(search)) ||
                    (a.Applicant.User.Login != null && a.Applicant.User.Login.ToLower().Contains(search)) ||
                    (a.Address != null && a.Address.ToLower().Contains(search)) ||
                    (a.CitizenComment != null && a.CitizenComment.ToLower().Contains(search)) ||
                    (a.CadastralObject != null && a.CadastralObject.Address != null &&
                     a.CadastralObject.Address.ToLower().Contains(search)) ||
                    (a.CadastralObject != null && a.CadastralObject.CadastralNumber != null &&
                     a.CadastralObject.CadastralNumber.Contains(search))
                );
            }

            var applications = await query
                .OrderByDescending(a => a.ApplicationDate)
                .ToListAsync();

            ViewBag.StatusFilter = status;
            ViewBag.TypeFilter = type;
            ViewBag.Search = search;
            ViewBag.TotalCount = await _context.Applications.CountAsync();
            ViewBag.PendingCount = await _context.Applications
                .CountAsync(a => a.ApplicationStatus == "Принят к проверке");
            ViewBag.InProgressCount = await _context.Applications
                .CountAsync(a => a.ApplicationStatus == "На проверке");
            ViewBag.CompletedCount = await _context.Applications
                .CountAsync(a => a.ApplicationStatus == "Одобрен" || a.ApplicationStatus == "Отклонен");

            return View(applications);
        }

        // GET: /Application/Verify/5 - страница проверки заявления
        public async Task<IActionResult> Verify(int id)
        {
            if (!IsEmployeeOrAdmin())
                return RedirectToAction("AccessDenied", "Home");

            var application = await _context.Applications
                .Include(a => a.CadastralObject)
                .Include(a => a.Applicant)
                    .ThenInclude(c => c.User)
                .Include(a => a.AssignedEmployee)
                    .ThenInclude(e => e.User)
                .Include(a => a.Attachments)
                .Include(a => a.ApplicationHistories)
                    .ThenInclude(h => h.ChangedByEmployee)
                        .ThenInclude(e => e.User)
                .FirstOrDefaultAsync(a => a.ApplicationId == id);

            if (application == null)
            {
                return NotFound();
            }

            // Если статус "Принят к проверке", меняем на "На проверке" и назначаем сотрудника
            if (application.ApplicationStatus == "Принят к проверке")
            {
                var employeeId = GetCurrentEmployeeId();
                application.ApplicationStatus = "На проверке";
                application.AssignedEmployeeId = employeeId;

                // Логируем изменение статуса
                var history = new ApplicationHistory
                {
                    ApplicationId = application.ApplicationId,
                    OldStatus = "Принят к проверке",
                    NewStatus = "На проверке",
                    ChangeDate = DateTime.Now,
                    ChangedByEmployeeId = employeeId,
                    HistoryComment = "Заявление взято на проверку"
                };
                _context.ApplicationHistories.Add(history);

                await _context.SaveChangesAsync();

                // Обновляем объект application
                application = await _context.Applications
                    .Include(a => a.CadastralObject)
                    .Include(a => a.Applicant)
                        .ThenInclude(c => c.User)
                    .Include(a => a.AssignedEmployee)
                        .ThenInclude(e => e.User)
                    .Include(a => a.Attachments)
                    .Include(a => a.ApplicationHistories)
                        .ThenInclude(h => h.ChangedByEmployee)
                            .ThenInclude(e => e.User)
                    .FirstOrDefaultAsync(a => a.ApplicationId == id);
            }

            ViewBag.CurrentEmployeeId = GetCurrentEmployeeId();
            return View(application);
        }

        // POST: /Application/Approve - одобрение заявления
        [HttpPost]
        public async Task<IActionResult> Approve(int id, string decisionComment)
        {
            if (!IsEmployeeOrAdmin())
                return RedirectToAction("AccessDenied", "Home");

            try
            {
                var application = await _context.Applications
                    .Include(a => a.Applicant)
                    .FirstOrDefaultAsync(a => a.ApplicationId == id);

                if (application == null)
                {
                    return NotFound();
                }

                var employeeId = GetCurrentEmployeeId();

                // Обновляем статус заявления
                var oldStatus = application.ApplicationStatus;
                application.ApplicationStatus = "Одобрен";
                application.DecisionComment = decisionComment;
                application.AssignedEmployeeId = employeeId;

                // Логируем изменение статуса
                var history = new ApplicationHistory
                {
                    ApplicationId = application.ApplicationId,
                    OldStatus = oldStatus,
                    NewStatus = application.ApplicationStatus,
                    ChangeDate = DateTime.Now,
                    ChangedByEmployeeId = employeeId,
                    HistoryComment = $"Заявление одобрено. Комментарий: {decisionComment}"
                };
                _context.ApplicationHistories.Add(history);

                // Если это регистрация нового объекта - создаем кадастровый объект
                if (application.ApplicationType == "Регистрация")
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
                    await _context.SaveChangesAsync(); // Сохраняем, чтобы получить ID

                    // Связываем заявление с созданным объектом
                    application.CadastralObjectId = newObject.CadastralObjectId;

                    // Создаем запись в истории создания объекта
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

                // Если это обновление существующего объекта
                else if (application.ApplicationType == "Обновление" && application.CadastralObjectId.HasValue)
                {
                    var cadastralObject = await _context.CadastralObjects
                        .FirstOrDefaultAsync(co => co.CadastralObjectId == application.CadastralObjectId);

                    if (cadastralObject != null)
                    {
                        // Получаем старые значения
                        var oldAddress = cadastralObject.Address;
                        var oldArea = cadastralObject.Area;
                        var oldType = cadastralObject.CadastralObjectType;

                        // Обновляем объект данными из заявления
                        cadastralObject.Address = application.Address;
                        cadastralObject.Area = application.Area;
                        cadastralObject.CadastralObjectType = application.CadastralObjectType;

                        // Логируем изменения в истории объекта
                        if (oldAddress != cadastralObject.Address)
                        {
                            var addressHistory = new CadastralObjectHistory
                            {
                                CadastralObjectId = cadastralObject.CadastralObjectId,
                                ChangedField = "Адрес",
                                OldValue = oldAddress,
                                NewValue = cadastralObject.Address,
                                ChangeDate = DateTime.Now,
                                ChangedByEmployeeId = employeeId
                            };
                            _context.CadastralObjectHistories.Add(addressHistory);
                        }

                        if (oldArea != cadastralObject.Area)
                        {
                            var areaHistory = new CadastralObjectHistory
                            {
                                CadastralObjectId = cadastralObject.CadastralObjectId,
                                ChangedField = "Площадь",
                                OldValue = oldArea.ToString("N2"),
                                NewValue = cadastralObject.Area.ToString("N2"),
                                ChangeDate = DateTime.Now,
                                ChangedByEmployeeId = employeeId
                            };
                            _context.CadastralObjectHistories.Add(areaHistory);
                        }

                        if (oldType != cadastralObject.CadastralObjectType)
                        {
                            var typeHistory = new CadastralObjectHistory
                            {
                                CadastralObjectId = cadastralObject.CadastralObjectId,
                                ChangedField = "Тип объекта",
                                OldValue = oldType,
                                NewValue = cadastralObject.CadastralObjectType,
                                ChangeDate = DateTime.Now,
                                ChangedByEmployeeId = employeeId
                            };
                            _context.CadastralObjectHistories.Add(typeHistory);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Заявление #{id} успешно одобрено!";
                return RedirectToAction("ViewAll");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при одобрении заявления: {ex.Message}";
                return RedirectToAction("Verify", new { id });
            }
        }

        // POST: /Application/Reject - отклонение заявления
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string decisionComment)
        {
            if (!IsEmployeeOrAdmin())
                return RedirectToAction("AccessDenied", "Home");

            try
            {
                var application = await _context.Applications.FindAsync(id);
                if (application == null)
                {
                    return NotFound();
                }

                var employeeId = GetCurrentEmployeeId();
                var oldStatus = application.ApplicationStatus;

                application.ApplicationStatus = "Отклонен";
                application.DecisionComment = decisionComment;
                application.AssignedEmployeeId = employeeId;

                // Логируем изменение статуса
                var history = new ApplicationHistory
                {
                    ApplicationId = application.ApplicationId,
                    OldStatus = oldStatus,
                    NewStatus = "Отклонен",
                    ChangeDate = DateTime.Now,
                    ChangedByEmployeeId = employeeId,
                    HistoryComment = $"Заявление отклонено. Причина: {decisionComment}"
                };
                _context.ApplicationHistories.Add(history);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Заявление #{id} отклонено.";
                return RedirectToAction("ViewAll");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при отклонении заявления: {ex.Message}";
                return RedirectToAction("Verify", new { id });
            }
        }

        // Вспомогательные методы
        private bool IsEmployeeOrAdmin()
        {
            var userType = HttpContext.Session.GetString("UserType");
            return userType == "Employee" || userType == "Admin";
        }

        private int GetCurrentEmployeeId()
        {
            var userId = HttpContext.Session.GetString("UserId");
            return int.TryParse(userId, out var id) ? id : 0;
        }

        private string GenerateUniqueCadastralNumber()
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
    }
}