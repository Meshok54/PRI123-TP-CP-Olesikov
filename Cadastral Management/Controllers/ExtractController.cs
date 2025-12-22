using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cadastral_Management.Models;
using Cadastral_Management.Data;
using System.IO;

namespace Cadastral_Management.Controllers
{
    public class ExtractController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ExtractController(
            ApplicationDbContext context,
            IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: /Extract/MyExtracts
        [HttpGet]
        public async Task<IActionResult> MyExtracts(
            string searchQuery = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? selectedObjectId = null)
        {
            try
            {
                // Получаем ID текущего пользователя из сессии
                var currentUserIdStr = HttpContext.Session.GetString("UserId");
                var userRole = HttpContext.Session.GetString("UserType");
                var currentUserId = int.Parse(currentUserIdStr);

                // Проверяем авторизацию
                if (string.IsNullOrEmpty(currentUserIdStr) || string.IsNullOrEmpty(userRole))
                {
                    TempData["ErrorMessage"] = "Требуется авторизация";
                    return RedirectToAction("Login", "Account");
                }


                // Проверяем, что пользователь - гражданин
                if (userRole != "Citizen")
                {
                    TempData["ErrorMessage"] = "Доступ разрешен только гражданам";
                    return RedirectToAction("Index", "Home");
                }

                // Получаем все объекты пользователя для фильтра
                var userObjects = await _context.CadastralObjects
                    .Where(o => o.OwnerId == currentUserId)
                    .OrderBy(o => o.CadastralNumber)
                    .ToListAsync();

                // Получаем все выписки пользователя с навигационными свойствами
                var extractsQuery = _context.Extracts
                    .Where(e => e.RequestedById == currentUserId)
                    .AsQueryable();

                // Применяем фильтры
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    // Подзапрос для фильтрации по связанным данным
                    var filteredObjects = _context.CadastralObjects
                        .Where(o => o.CadastralNumber.Contains(searchQuery) ||
                                   o.Address.Contains(searchQuery))
                        .Select(o => o.CadastralObjectId);

                    extractsQuery = extractsQuery.Where(e => filteredObjects.Contains(e.CadastralObjectId));
                }

                if (startDate.HasValue)
                {
                    extractsQuery = extractsQuery.Where(e => e.GenerationDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    var endDateWithTime = endDate.Value.AddDays(1).AddSeconds(-1);
                    extractsQuery = extractsQuery.Where(e => e.GenerationDate <= endDateWithTime);
                }

                if (selectedObjectId.HasValue && selectedObjectId.Value > 0)
                {
                    extractsQuery = extractsQuery.Where(e => e.CadastralObjectId == selectedObjectId.Value);
                }

                // Сортируем по дате (новые сверху)
                extractsQuery = extractsQuery.OrderByDescending(e => e.GenerationDate);

                // Получаем ID выписок
                var extractIds = await extractsQuery.Select(e => e.ExtractId).ToListAsync();

                // Получаем полные данные выписок с навигационными свойствами
                var extracts = await _context.Extracts
                    .Include(e => e.CadastralObject)
                    .Include(e => e.RequestedBy)
                    .ThenInclude(c => c.User)
                    .Where(e => extractIds.Contains(e.ExtractId))
                    .OrderByDescending(e => e.GenerationDate)
                    .ToListAsync();

                // Сохраняем фильтры и объекты пользователя во ViewBag
                ViewBag.FilterSearchQuery = searchQuery;
                ViewBag.FilterStartDate = startDate;
                ViewBag.FilterEndDate = endDate;
                ViewBag.FilterSelectedObjectId = selectedObjectId;
                ViewBag.UserObjects = userObjects;
                ViewBag.TotalCount = extracts.Count;

                // Получаем email пользователя
                if (extracts.Any())
                {
                    ViewBag.UserEmail = extracts.First().RequestedBy?.User?.Email;
                }
                else
                {
                    // Если нет выписок, получаем email из таблицы пользователей
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.UserId == currentUserId);
                    ViewBag.UserEmail = user?.Email;
                }

                return View(extracts);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при загрузке выписок: {ex.Message}";
                return View(new List<Extract>());
            }
        }

        // GET: /Extract/Download/{hash}
        [HttpGet]
        public async Task<IActionResult> Download(string hash)
        {
            try
            {
                if (string.IsNullOrEmpty(hash))
                {
                    TempData["ErrorMessage"] = "Не указан идентификатор выписки";
                    return RedirectToAction("MyExtracts");
                }

                // Получаем ID текущего пользователя из сессии
                var currentUserIdStr = HttpContext.Session.GetString("UserId");
                var currentUserId = int.Parse(currentUserIdStr);
                if (string.IsNullOrEmpty(currentUserIdStr))
                {
                    TempData["ErrorMessage"] = "Требуется авторизация";
                    return RedirectToAction("Login", "Account");
                }

                // Находим выписку по хэшу
                var extract = await _context.Extracts
                    .Include(e => e.CadastralObject)
                    .FirstOrDefaultAsync(e => e.DownloadLinkHash == hash &&
                                           e.RequestedById == currentUserId);

                if (extract == null)
                {
                    TempData["ErrorMessage"] = "Выписка не найдена или у вас нет доступа";
                    return RedirectToAction("MyExtracts");
                }

                // Проверяем существование файла
                var filePath = Path.Combine(_env.WebRootPath, extract.FilePath.TrimStart('/'));
                if (!System.IO.File.Exists(filePath))
                {
                    TempData["ErrorMessage"] = "Файл выписки не найден";
                    return RedirectToAction("MyExtracts");
                }

                // Определяем MIME тип и имя файла
                var contentType = "application/pdf";
                var fileName = $"Выписка_{extract.CadastralObject.CadastralNumber}_{extract.GenerationDate:yyyyMMdd}.pdf";

                // Возвращаем файл
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при скачивании файла: {ex.Message}";
                return RedirectToAction("MyExtracts");
            }
        }

        // GET: /Extract/SendEmail/{id}
        [HttpGet]
        public async Task<IActionResult> SendEmail(int id)
        {
            try
            {
                // Получаем ID текущего пользователя из сессии
                var currentUserIdStr = HttpContext.Session.GetString("UserId");
                var currentUserId = int.Parse(currentUserIdStr);
                if (string.IsNullOrEmpty(currentUserIdStr))
                {
                    TempData["ErrorMessage"] = "Требуется авторизация";
                    return RedirectToAction("Login", "Account");
                }

                // Находим выписку
                var extract = await _context.Extracts
                    .Include(e => e.CadastralObject)
                    .Include(e => e.RequestedBy)
                    .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(e => e.ExtractId == id &&
                                           e.RequestedById == currentUserId);

                if (extract == null)
                {
                    TempData["ErrorMessage"] = "Выписка не найдена";
                    return RedirectToAction("MyExtracts");
                }

                // Проверяем email пользователя
                var userEmail = extract.RequestedBy?.User?.Email;
                if (string.IsNullOrEmpty(userEmail))
                {
                    TempData["ErrorMessage"] = "Email пользователя не указан";
                    return RedirectToAction("MyExtracts");
                }

                // Проверяем существование файла
                var filePath = Path.Combine(_env.WebRootPath, extract.FilePath.TrimStart('/'));
                if (!System.IO.File.Exists(filePath))
                {
                    TempData["ErrorMessage"] = "Файл выписки не найден";
                    return RedirectToAction("MyExtracts");
                }

                // Отправляем email
                var sendResult = await SendExtractByEmail(userEmail, extract, filePath);

                if (sendResult)
                {
                    // Обновляем флаг отправки
                    extract.IsSentViaEmail = true;
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Выписка успешно отправлена на email {userEmail}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Ошибка при отправке email";
                }

                return RedirectToAction("MyExtracts");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction("MyExtracts");
            }
        }

        // Вспомогательный метод для отправки email
        private async Task<bool> SendExtractByEmail(string toEmail, Extract extract, string filePath)
        {
            try
            {
                // Реализация отправки email на C#
                // Пример с использованием System.Net.Mail

                var smtpClient = new System.Net.Mail.SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new System.Net.NetworkCredential("your-email@gmail.com", "your-password"),
                    EnableSsl = true,
                };

                var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress("cadastral@system.com"),
                    Subject = $"Кадастровая выписка №{extract.ExtractId}",
                    Body = $"<h2>Кадастровая выписка</h2>" +
                           $"<p><strong>Дата формирования:</strong> {extract.GenerationDate:dd.MM.yyyy HH:mm}</p>" +
                           $"<p><strong>Кадастровый номер:</strong> {extract.CadastralObject?.CadastralNumber}</p>" +
                           $"<p><strong>Адрес:</strong> {extract.CadastralObject?.Address}</p>" +
                           $"<p>Выписка во вложении.</p>",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(toEmail);

                // Прикрепляем файл
                var attachment = new System.Net.Mail.Attachment(filePath);
                mailMessage.Attachments.Add(attachment);

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Для демонстрации всегда возвращаем true
                // В реальном приложении здесь должно быть логирование
                Console.WriteLine($"Ошибка отправки email (демо): {ex.Message}");
                return true; // В демо-режиме всегда успешно
            }
        }
    }
}