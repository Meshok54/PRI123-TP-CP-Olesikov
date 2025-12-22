using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Cadastral_Management.Controllers
{
    public class CadastralObjectController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CadastralObjectController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /CadastralObject/MyCadastralObjects
        public async Task<IActionResult> MyCadastralObjects()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var citizenId = int.Parse(userId);

            // Получаем все кадастровые объекты пользователя
            var cadastralObjects = await _context.CadastralObjects
                .Where(co => co.OwnerId == citizenId)
                .OrderByDescending(co => co.RegistrationDate)
                .ToListAsync();

            // Получаем последние выписки для каждого объекта
            var extracts = await _context.Extracts
                .Where(e => e.RequestedById == citizenId)
                .OrderByDescending(e => e.GenerationDate)
                .ToListAsync();

            ViewBag.Extracts = extracts;
            return View(cadastralObjects);
        }

        // POST: /CadastralObject/RequestExtract - запрос выписки (синхронный)
        [HttpPost]
        public async Task<IActionResult> RequestExtract(int objectId)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Требуется авторизация";
                    return RedirectToAction("Login", "Account");
                }

                var citizenId = int.Parse(userId);

                // Проверяем, что объект принадлежит пользователю
                var cadastralObject = await _context.CadastralObjects
                    .Include(co => co.Owner)
                        .ThenInclude(o => o.User)
                    .FirstOrDefaultAsync(co => co.CadastralObjectId == objectId && co.OwnerId == citizenId);

                if (cadastralObject == null)
                {
                    TempData["ErrorMessage"] = "Объект не найден или нет доступа";
                    return RedirectToAction("MyCadastralObjects");
                }

                // Получаем историю изменений объекта
                var objectHistory = await _context.CadastralObjectHistories
                    .Include(h => h.ChangedByEmployee)
                        .ThenInclude(e => e.User)
                    .Where(h => h.CadastralObjectId == objectId)
                    .OrderByDescending(h => h.ChangeDate)
                    .ToListAsync();

                // Генерируем уникальный хэш для ссылки
                var hash = GenerateDownloadHash();

                // Создаем путь для сохранения файла
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "extracts");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // Заменяем двоеточия в кадастровом номере на допустимые символы
                var safeCadastralNumber = cadastralObject.CadastralNumber.Replace(":", "_");

                var fileName = $"extract_{Guid.NewGuid()}.pdf"; // Используем GUID вместо кадастрового номера
                var filePath = Path.Combine(uploadsPath, fileName);
                var relativePath = $"/uploads/extracts/{fileName}";

                // Создаем PDF выписку
                await GenerateExtractPdf(cadastralObject, objectHistory, filePath);

                // Создаем запись в базе данных
                var extract = new Extract
                {
                    GenerationDate = DateTime.Now,
                    CadastralObjectId = objectId,
                    RequestedById = citizenId,
                    FilePath = relativePath,
                    DownloadLinkHash = hash,
                    IsSentViaEmail = false
                };

                _context.Extracts.Add(extract);
                await _context.SaveChangesAsync();

                // Сохраняем ID выписки в сессии вместо TempData
                HttpContext.Session.SetInt32("LastExtractId", extract.ExtractId);

                TempData["SuccessMessage"] = $"Выписка по объекту {cadastralObject.CadastralNumber} успешно сформирована!";
                // Просто всегда устанавливаем флаг
                ViewBag.ShowDownloadButton = true;

                return RedirectToAction("MyCadastralObjects");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при формировании выписки: {ex.Message}";
                return RedirectToAction("MyCadastralObjects");
            }
        }

        // GET: /CadastralObject/DownloadLatestExtract - скачивание последней выписки
        public async Task<IActionResult> DownloadLatestExtract()
        {
            var extractId = HttpContext.Session.GetInt32("LastExtractId");
            if (!extractId.HasValue)
            {
                TempData["ErrorMessage"] = "Выписка не найдена";
                return RedirectToAction("MyCadastralObjects");
            }

            var extract = await _context.Extracts
                .Include(e => e.CadastralObject)
                .FirstOrDefaultAsync(e => e.ExtractId == extractId.Value);

            if (extract == null)
            {
                TempData["ErrorMessage"] = "Выписка не найдена";
                return RedirectToAction("MyCadastralObjects");
            }

            return await DownloadExtract(extract.DownloadLinkHash);
        }

        // GET: /CadastralObject/DownloadExtract/{hash} - скачивание выписки
        public async Task<IActionResult> DownloadExtract(string hash)
        {
            var extract = await _context.Extracts
                .Include(e => e.CadastralObject)
                .FirstOrDefaultAsync(e => e.DownloadLinkHash == hash);

            if (extract == null)
            {
                return NotFound();
            }

            // Проверяем права доступа
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId) || int.Parse(userId) != extract.RequestedById)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", extract.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                // Попробуем найти файл с замененными двоеточиями
                var safeFileName = extract.FilePath.Replace(":", "_");
                filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", safeFileName.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Файл не найден");
                }
            }

            var contentType = "application/pdf";
            var safeCadastralNumber = extract.CadastralObject.CadastralNumber.Replace(":", "_");
            var fileName = $"Выписка_{safeCadastralNumber}_{extract.GenerationDate:yyyyMMdd}.pdf";

            return PhysicalFile(filePath, contentType, fileName);
        }

        // GET: /CadastralObject/CreateUpdateApplication - переход к форме обновления
        public async Task<IActionResult> CreateUpdateApplication(int objectId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var citizenId = int.Parse(userId);

            // Проверяем, что объект принадлежит пользователю
            var cadastralObject = await _context.CadastralObjects
                .FirstOrDefaultAsync(co => co.CadastralObjectId == objectId && co.OwnerId == citizenId);

            if (cadastralObject == null)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Сохраняем данные объекта в сессию для предзаполнения формы
            HttpContext.Session.SetString("UpdateObjectId", objectId.ToString());
            HttpContext.Session.SetString("UpdateCadastralNumber", cadastralObject.CadastralNumber);
            HttpContext.Session.SetString("UpdateAddress", cadastralObject.Address);
            HttpContext.Session.SetString("UpdateArea", cadastralObject.Area.ToString());
            HttpContext.Session.SetString("UpdateObjectType", cadastralObject.CadastralObjectType);

            return RedirectToAction("Create", "Application");
        }

        // GET: /CadastralObject/Create
        public IActionResult Create()
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Home");

            return View();
        }

        // POST: /CadastralObject/Create
        [HttpPost]
        public async Task<IActionResult> Create(
            string cadastralNumber,
            string address,
            string area,
            string objectType,
            int ownerId,
            DateTime registrationDate)
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Home");

            try
            {
                // Проверяем уникальность кадастрового номера
                if (await _context.CadastralObjects.AnyAsync(co => co.CadastralNumber == cadastralNumber))
                {
                    ModelState.AddModelError("", "Объект с таким кадастровым номером уже существует");
                    return View();
                }

                // Проверяем существование владельца
                var owner = await _context.Citizens.FindAsync(ownerId);
                if (owner == null)
                {
                    ModelState.AddModelError("", "Владелец с указанным ID не найден");
                    return View();
                }

                // Валидация
                if (string.IsNullOrWhiteSpace(area))
                {
                    ModelState.AddModelError("area", "Поле 'Площадь' обязательно для заполнения");
                    return View();
                }

                // Нормализация
                string normalizedArea = area.Replace(',', '.').Trim();

                if (!decimal.TryParse(normalizedArea,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out decimal areaDecimal) || areaDecimal <= 0)
                {
                    ModelState.AddModelError("area",
                        "Некорректное значение площади. Используйте положительное число в формате: 100.50");
                    return View();
                }

                var cadastralObject = new CadastralObject
                {
                    CadastralNumber = cadastralNumber,
                    Address = address,
                    Area = areaDecimal,
                    CadastralObjectType = objectType,
                    OwnerId = ownerId,
                    RegistrationDate = registrationDate,
                    CreatedAt = DateTime.Now
                };

                _context.CadastralObjects.Add(cadastralObject);
                await _context.SaveChangesAsync();

                // Создаем запись в истории
                var history = new CadastralObjectHistory
                {
                    CadastralObjectId = cadastralObject.CadastralObjectId,
                    ChangedField = "Создание",
                    OldValue = null,
                    NewValue = "Объект создан",
                    ChangeDate = DateTime.Now,
                    ChangedByEmployeeId = GetCurrentEmployeeId()
                };

                _context.CadastralObjectHistories.Add(history);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Объект {cadastralNumber} успешно создан!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Ошибка при создании объекта: {ex.Message}");
                return View();
            }
        }


        // GET: /CadastralObject/Details/5
        public async Task<IActionResult> Details(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Home");

            var cadastralObject = await _context.CadastralObjects
                .Include(co => co.Owner)
                    .ThenInclude(o => o.User)
                .Include(co => co.Extracts)
                .Include(co => co.CadastralObjectHistories)
                    .ThenInclude(h => h.ChangedByEmployee)
                        .ThenInclude(e => e.User)
                .Include(co => co.Applications)
                .FirstOrDefaultAsync(co => co.CadastralObjectId == id);

            if (cadastralObject == null)
            {
                return NotFound();
            }

            return View(cadastralObject);
        }

        // GET: /CadastralObject/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Home");

            var cadastralObject = await _context.CadastralObjects
                .Include(co => co.Owner)
                    .ThenInclude(o => o.User)
                .FirstOrDefaultAsync(co => co.CadastralObjectId == id);

            if (cadastralObject == null)
            {
                return NotFound();
            }

            return View(cadastralObject);
        }

        // POST: /CadastralObject/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(
            int id,
            string cadastralNumber,
            string address,
            decimal area,
            string objectType,
            int ownerId,
            DateTime registrationDate)
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Home");

            // Получаем объект для отображения в форме
            var existingObject = await _context.CadastralObjects
                .Include(co => co.Owner)
                    .ThenInclude(o => o.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(co => co.CadastralObjectId == id);

            if (existingObject == null)
            {
                return NotFound();
            }

            try
            {
                // Проверяем существование владельца
                var owner = await _context.Citizens.FindAsync(ownerId);
                if (owner == null)
                {
                    ModelState.AddModelError("ownerId", "Владелец с указанным ID не найден");
                    // Возвращаем исходный объект с ошибкой
                    return View(existingObject);
                }

                // Теперь получаем объект для обновления
                var cadastralObject = await _context.CadastralObjects.FindAsync(id);
                if (cadastralObject == null)
                {
                    return NotFound();
                }

                // Сохраняем старые значения для истории
                var oldValues = new Dictionary<string, string>
        {
            { "Address", cadastralObject.Address },
            { "Area", cadastralObject.Area.ToString() },
            { "CadastralObjectType", cadastralObject.CadastralObjectType },
            { "OwnerId", cadastralObject.OwnerId.ToString() }
        };

                // Обновляем объект
                cadastralObject.CadastralNumber = cadastralNumber;
                cadastralObject.Address = address;
                cadastralObject.Area = area;
                cadastralObject.CadastralObjectType = objectType;
                cadastralObject.OwnerId = ownerId;
                cadastralObject.RegistrationDate = registrationDate;

                _context.Update(cadastralObject);
                await _context.SaveChangesAsync();

                // Создаем записи в истории
                var employeeId = GetCurrentEmployeeId();
                foreach (var change in oldValues)
                {
                    var newValue = typeof(CadastralObject).GetProperty(change.Key)?.GetValue(cadastralObject)?.ToString();
                    if (change.Value != newValue)
                    {
                        var history = new CadastralObjectHistory
                        {
                            CadastralObjectId = id,
                            ChangedField = change.Key,
                            OldValue = change.Value,
                            NewValue = newValue,
                            ChangeDate = DateTime.Now,
                            ChangedByEmployeeId = employeeId
                        };
                        _context.CadastralObjectHistories.Add(history);
                    }
                }
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Объект успешно обновлен!";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Ошибка при обновлении: {ex.Message}");
                return View(existingObject);
            }
        }

        // GET: /CadastralObject/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("AccessDenied", "Home");

            try
            {
                var cadastralObject = await _context.CadastralObjects
                    .Include(co => co.Owner)
                        .ThenInclude(o => o.User)
                    .FirstOrDefaultAsync(co => co.CadastralObjectId == id);

                if (cadastralObject == null)
                {
                    return NotFound();
                }

                // Проверяем, нет ли связанных заявлений
                var hasApplications = await _context.Applications.AnyAsync(a => a.CadastralObjectId == id);
                if (hasApplications)
                {
                    TempData["ErrorMessage"] = "Нельзя удалить объект, у которого есть связанные заявления!";
                    return RedirectToAction("Index", "Home");
                }

                _context.CadastralObjects.Remove(cadastralObject);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Объект {cadastralObject.CadastralNumber} успешно удален!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при удалении: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }

        // Вспомогательные методы
        private bool IsAuthorized()
        {
            var userType = HttpContext.Session.GetString("UserType");
            return userType == "Admin" || userType == "Employee";
        }

        private int GetCurrentEmployeeId()
        {
            var userId = HttpContext.Session.GetString("UserId");
            return int.TryParse(userId, out var id) ? id : 1;
        }


        private string GenerateDownloadHash()
        {
            using (var sha256 = SHA256.Create())
            {
                var data = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString() + DateTime.Now.Ticks);
                var hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private async Task GenerateExtractPdf(CadastralObject obj, List<CadastralObjectHistory> history, string filePath)
        {
            try
            {
                // Создаем HTML контент
                var htmlContent = GenerateExtractHtml(obj, history);

                // Сохраняем HTML файл (временный) для отладки
                var htmlFilePath = filePath.Replace(".pdf", ".html");
                await System.IO.File.WriteAllTextAsync(htmlFilePath, htmlContent, Encoding.UTF8);

                // Создаем PDF с использованием iTextSharp
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    using (var document = new Document(PageSize.A4, 50, 50, 50, 50))
                    {
                        var writer = PdfWriter.GetInstance(document, stream);
                        document.Open();

                        // Добавляем UTF-8 поддержку для русского текста
                        string arialuniTff = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "ARIALUNI.TTF");
                        string arialTff = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");

                        BaseFont baseFont;
                        if (System.IO.File.Exists(arialuniTff))
                        {
                            baseFont = BaseFont.CreateFont(arialuniTff, BaseFont.IDENTITY_H, BaseFont.NOT_EMBEDDED);
                        }
                        else if (System.IO.File.Exists(arialTff))
                        {
                            baseFont = BaseFont.CreateFont(arialTff, BaseFont.IDENTITY_H, BaseFont.NOT_EMBEDDED);
                        }
                        else
                        {
                            baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        }

                        var font = new Font(baseFont, 10, Font.NORMAL);
                        var boldFont = new Font(baseFont, 10, Font.BOLD);
                        var titleFont = new Font(baseFont, 14, Font.BOLD);
                        var headerFont = new Font(baseFont, 12, Font.BOLD);

                        // Заголовок
                        var title = new Paragraph("ВЫПИСКА ИЗ ГОСУДАРСТВЕННОГО КАДАСТРА НЕДВИЖИМОСТИ", titleFont)
                        {
                            Alignment = Element.ALIGN_CENTER,
                            SpacingAfter = 20f
                        };
                        document.Add(title);

                        var dateParagraph = new Paragraph($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}", font)
                        {
                            Alignment = Element.ALIGN_CENTER,
                            SpacingAfter = 30f
                        };
                        document.Add(dateParagraph);

                        // Раздел 1: Основные сведения
                        document.Add(new Paragraph("1. ОСНОВНЫЕ СВЕДЕНИЯ ОБ ОБЪЕКТЕ НЕДВИЖИМОСТИ", headerFont)
                        {
                            SpacingAfter = 10f
                        });

                        var table1 = new PdfPTable(2)
                        {
                            WidthPercentage = 100,
                            SpacingBefore = 10f,
                            SpacingAfter = 20f
                        };
                        table1.SetWidths(new float[] { 40, 60 });

                        AddTableRow(table1, "Кадастровый номер:", obj.CadastralNumber, font, boldFont);
                        AddTableRow(table1, "Адрес:", obj.Address, font, boldFont);
                        AddTableRow(table1, "Тип объекта:", obj.CadastralObjectType, font, boldFont);
                        AddTableRow(table1, "Площадь, кв.м:", obj.Area.ToString("N2"), font, boldFont);
                        AddTableRow(table1, "Дата регистрации:", obj.RegistrationDate.ToString("dd.MM.yyyy"), font, boldFont);

                        document.Add(table1);

                        // Раздел 2: Сведения о правообладателе
                        document.Add(new Paragraph("2. СВЕДЕНИЯ О ПРАВООБЛАДАТЕЛЕ", headerFont)
                        {
                            SpacingAfter = 10f
                        });

                        var table2 = new PdfPTable(2)
                        {
                            WidthPercentage = 100,
                            SpacingBefore = 10f,
                            SpacingAfter = 20f
                        };
                        table2.SetWidths(new float[] { 40, 60 });

                        AddTableRow(table2, "ФИО:", obj.Owner.User.FullName, font, boldFont);
                        AddTableRow(table2, "Паспортные данные:", obj.Owner.PassportData, font, boldFont);

                        document.Add(table2);

                        // Раздел 3: История изменений (если есть)
                        if (history.Any())
                        {
                            document.Add(new Paragraph("3. ИСТОРИЯ ИЗМЕНЕНИЙ ОБЪЕКТА", headerFont)
                            {
                                SpacingAfter = 10f
                            });

                            var table3 = new PdfPTable(5)
                            {
                                WidthPercentage = 100,
                                SpacingBefore = 10f,
                                SpacingAfter = 20f
                            };
                            table3.SetWidths(new float[] { 20, 20, 20, 20, 20 });

                            // Заголовки таблицы
                            AddTableHeader(table3, "Дата", boldFont);
                            AddTableHeader(table3, "Измененное поле", boldFont);
                            AddTableHeader(table3, "Старое значение", boldFont);
                            AddTableHeader(table3, "Новое значение", boldFont);
                            AddTableHeader(table3, "Исполнитель", boldFont);

                            // Данные истории
                            foreach (var h in history)
                            {
                                AddTableCell(table3, h.ChangeDate.ToString("dd.MM.yyyy HH:mm"), font);
                                AddTableCell(table3, h.ChangedField, font);
                                AddTableCell(table3, h.OldValue ?? "-", font);
                                AddTableCell(table3, h.NewValue ?? "-", font);
                                AddTableCell(table3, h.ChangedByEmployee?.User?.FullName ?? "Система", font);
                            }

                            document.Add(table3);
                        }

                        // Раздел 4: Налоговая информация
                        document.Add(new Paragraph("4. СВЕДЕНИЯ ДЛЯ НАЛОГООБЛОЖЕНИЯ", headerFont)
                        {
                            SpacingAfter = 10f
                        });

                        decimal cadastralValue = obj.Area * 100000;
                        decimal taxPerYear = cadastralValue * 0.003m;

                        var table4 = new PdfPTable(2)
                        {
                            WidthPercentage = 100,
                            SpacingBefore = 10f,
                            SpacingAfter = 10f
                        };
                        table4.SetWidths(new float[] { 40, 60 });

                        AddTableRow(table4, "Кадастровая стоимость:", cadastralValue.ToString("N2") + " руб.", font, boldFont);
                        AddTableRow(table4, "Ставка налога:", "0.3%", font, boldFont);
                        AddTableRow(table4, "Сумма налога в год:", taxPerYear.ToString("N2") + " руб.", font, boldFont);

                        document.Add(table4);

                        document.Add(new Paragraph("* Приведенные данные носят информационный характер. Точную сумму налога уточняйте в ФНС.", new Font(baseFont, 8, Font.ITALIC))
                        {
                            SpacingBefore = 5f,
                            SpacingAfter = 30f
                        });

                        // Печать
                        var stamp = new Paragraph("Выписка сформирована\nэлектронно-цифровой подписью\n\n" +
                                                 "Государственная информационная система\n\"Кадастровое управление\"",
                                                 new Font(baseFont, 9, Font.NORMAL))
                        {
                            Alignment = Element.ALIGN_RIGHT
                        };
                        document.Add(stamp);

                        document.Add(new Chunk("\n"));

                        // Подвал
                        var footer = new Paragraph($"Выписка действительна в течение 30 дней с даты формирования\n" +
                                                  $"Данные актуальны на: {DateTime.Now:dd.MM.yyyy HH:mm}",
                                                  new Font(baseFont, 8, Font.NORMAL))
                        {
                            Alignment = Element.ALIGN_CENTER,
                            SpacingBefore = 50f
                        };
                        document.Add(footer);

                        document.Close();
                    }
                }

                // Удаляем временный HTML файл
                if (System.IO.File.Exists(htmlFilePath))
                {
                    System.IO.File.Delete(htmlFilePath);
                }
            }
            catch (Exception ex)
            {
                // Создаем простой текстовый файл в случае ошибки
                await System.IO.File.WriteAllTextAsync(filePath,
                    $"Ошибка при создании выписки: {ex.Message}\n\n" +
                    $"Кадастровый номер: {obj.CadastralNumber}\n" +
                    $"Адрес: {obj.Address}\n" +
                    $"Дата: {DateTime.Now}");
            }
        }

        // Вспомогательные методы для создания таблиц
        private void AddTableRow(PdfPTable table, string label, string value, Font normalFont, Font boldFont)
        {
            var labelCell = new PdfPCell(new Phrase(label, boldFont))
            {
                BackgroundColor = new BaseColor(245, 245, 245),
                Padding = 8,
                BorderWidth = 1
            };

            var valueCell = new PdfPCell(new Phrase(value, normalFont))
            {
                Padding = 8,
                BorderWidth = 1
            };

            table.AddCell(labelCell);
            table.AddCell(valueCell);
        }

        private void AddTableHeader(PdfPTable table, string text, Font font)
        {
            var cell = new PdfPCell(new Phrase(text, font))
            {
                BackgroundColor = new BaseColor(200, 200, 200),
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 8,
                BorderWidth = 1
            };
            table.AddCell(cell);
        }

        private void AddTableCell(PdfPTable table, string text, Font font)
        {
            var cell = new PdfPCell(new Phrase(text, font))
            {
                Padding = 6,
                BorderWidth = 1
            };
            table.AddCell(cell);
        }
        // Метод для генерации HTML (оставляем для отладки)
        private string GenerateExtractHtml(CadastralObject obj, List<CadastralObjectHistory> history)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='ru'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Выписка из кадастра</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine(".header { text-align: center; margin-bottom: 40px; }");
            sb.AppendLine(".section { margin-bottom: 30px; }");
            sb.AppendLine(".section h2 { border-bottom: 2px solid #333; padding-bottom: 10px; }");
            sb.AppendLine(".info-table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
            sb.AppendLine(".info-table td { padding: 10px; border: 1px solid #ddd; }");
            sb.AppendLine(".info-table .label { font-weight: bold; width: 40%; background-color: #f5f5f5; }");
            sb.AppendLine(".footer { margin-top: 50px; text-align: center; font-size: 12px; color: #666; }");
            sb.AppendLine(".stamp { float: right; text-align: center; border: 2px solid #333; padding: 20px; margin-top: 50px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Заголовок
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>ВЫПИСКА ИЗ ГОСУДАРСТВЕННОГО КАДАСТРА НЕДВИЖИМОСТИ</h1>");
            sb.AppendLine("<p>Дата формирования: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</p>");
            sb.AppendLine("</div>");

            // Раздел 1: Основные сведения об объекте
            sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h2>1. ОСНОВНЫЕ СВЕДЕНИЯ ОБ ОБЪЕКТЕ НЕДВИЖИМОСТИ</h2>");
                sb.AppendLine("<table class='info-table'>");
                sb.AppendLine("<tr><td class='label'>Кадастровый номер</td><td>" + obj.CadastralNumber + "</td></tr>");
                sb.AppendLine("<tr><td class='label'>Адрес</td><td>" + obj.Address + "</td></tr>");
                sb.AppendLine("<tr><td class='label'>Тип объекта</td><td>" + obj.CadastralObjectType + "</td></tr>");
                sb.AppendLine("<tr><td class='label'>Площадь, кв.м</td><td>" + obj.Area.ToString("N2") + "</td></tr>");
                sb.AppendLine("<tr><td class='label'>Дата регистрации</td><td>" + obj.RegistrationDate.ToString("dd.MM.yyyy") + "</td></tr>");
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");

                // Раздел 2: Сведения о правообладателе
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h2>2. СВЕДЕНИЯ О ПРАВООБЛАДАТЕЛЕ</h2>");
                sb.AppendLine("<table class='info-table'>");
                sb.AppendLine("<tr><td class='label'>ФИО</td><td>" + obj.Owner.User.FullName + "</td></tr>");
                sb.AppendLine("<tr><td class='label'>Паспортные данные</td><td>" + obj.Owner.PassportData + "</td></tr>");
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");

                // Раздел 3: История изменений
                if (history.Any())
                {
                    sb.AppendLine("<div class='section'>");
                    sb.AppendLine("<h2>3. ИСТОРИЯ ИЗМЕНЕНИЙ ОБЪЕКТА</h2>");
                    sb.AppendLine("<table class='info-table'>");
                    sb.AppendLine("<thead><tr><th>Дата</th><th>Измененное поле</th><th>Старое значение</th><th>Новое значение</th><th>Исполнитель</th></tr></thead>");
                    sb.AppendLine("<tbody>");

                    foreach (var h in history)
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine("<td>" + h.ChangeDate.ToString("dd.MM.yyyy HH:mm") + "</td>");
                        sb.AppendLine("<td>" + h.ChangedField + "</td>");
                        sb.AppendLine("<td>" + (h.OldValue ?? "-") + "</td>");
                        sb.AppendLine("<td>" + (h.NewValue ?? "-") + "</td>");
                        sb.AppendLine("<td>" + (h.ChangedByEmployee?.User?.FullName ?? "Система") + "</td>");
                        sb.AppendLine("</tr>");
                    }

                    sb.AppendLine("</tbody>");
                    sb.AppendLine("</table>");
                    sb.AppendLine("</div>");
                }

                // Раздел 4: Налоговая информация (примерная)
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("<h2>4. СВЕДЕНИЯ ДЛЯ НАЛОГООБЛОЖЕНИЯ</h2>");
                sb.AppendLine("<table class='info-table'>");

                // Исправляем умножение - приводим 0.003 к типу decimal
                decimal cadastralValue = obj.Area * 100000;
                decimal taxRate = 0.003m; // m - суффикс для decimal
                decimal taxPerYear = cadastralValue * taxRate;

                sb.AppendLine("<tr><td class='label'>Кадастровая стоимость</td><td>" + cadastralValue.ToString("N2") + " руб.</td></tr>");
                sb.AppendLine("<tr><td class='label'>Ставка налога</td><td>0.3%</td></tr>");
                sb.AppendLine("<tr><td class='label'>Сумма налога в год</td><td>" + taxPerYear.ToString("N2") + " руб.</td></tr>");
                sb.AppendLine("</table>");
                sb.AppendLine("<p><small>* Приведенные данные носят информационный характер. Точную сумму налога уточняйте в ФНС.</small></p>");
                sb.AppendLine("</div>");

                // Печать
                sb.AppendLine("<div class='stamp'>");
                sb.AppendLine("<p>Выписка сформирована<br>электронно-цифровой подписью</p>");
                sb.AppendLine("<p>Государственная информационная система<br>\"Кадастровое управление\"</p>");
                sb.AppendLine("</div>");

                // Подвал
                sb.AppendLine("<div class='footer'>");
                sb.AppendLine("<p>Выписка действительна в течение 30 дней с даты формирования</p>");
                sb.AppendLine("<p>Данные актуальны на: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</p>");
                sb.AppendLine("</div>");

                sb.AppendLine("</body>");
                sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}