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
            string citizenComment)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var citizenId = int.Parse(userId);

                if (applicationType == "Обновление" && string.IsNullOrEmpty(cadastralNumber))
                {
                    ViewBag.Error = "Для заявления типа 'Обновление' необходимо указать кадастровый номер";
                    return View();
                }

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
                    application.CitizenComment = $"НОВЫЙ ОБЪЕКТ: {address}, {area} кв.м, {objectType}. " +
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

                _context.Applications.Add(application);
                await _context.SaveChangesAsync();

                ViewBag.Success = "Заявление успешно подано! Номер вашего заявления: " + application.ApplicationId;

                ModelState.Clear();

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Произошла ошибка при подаче заявления. Попробуйте еще раз.";
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
                .Where(a => a.ApplicantId == citizenId)
                .OrderByDescending(a => a.ApplicationDate)
                .ToListAsync();

            return View(applications);
        }
    }
}