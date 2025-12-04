using System.Globalization;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cadastral_Management.Controllers
{
    public class CadastralObjectController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CadastralObjectController(ApplicationDbContext context)
        {
            _context = context;
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
            return int.TryParse(userId, out var id) ? id : 1; // Заглушка
        }
    }
}