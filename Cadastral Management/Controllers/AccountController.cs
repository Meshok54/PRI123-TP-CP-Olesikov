using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using BCrypt.Net;

namespace Cadastral_Management.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login - показывает форму входа
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login - обрабатывает форму входа
        [HttpPost]
        public async Task<IActionResult> Login(string login, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Login == login);

                if (user == null)
                {
                    Console.WriteLine("логин не верный");
                    ViewBag.Error = "Пользователь с таким логином не найден";
                    return View();
                }

                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    ViewBag.Error = "Неверный пароль";
                    return View();
                }

                HttpContext.Session.SetString("UserId", user.UserId.ToString());
                HttpContext.Session.SetString("UserName", user.FullName);
                HttpContext.Session.SetString("UserType", user.UserType);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ОШИБКА ВХОДА ===");
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.WriteLine($"====================");

                ViewBag.Error = "Произошла ошибка при входе. Попробуйте еще раз.";
                return View();
            }
        }

        // GET: /Account/Register - показывает форму регистрации
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register - обрабатывает регистрацию
        [HttpPost]
        public async Task<IActionResult> Register(
            string login,
            string password,
            string fullName,
            string email,
            string phoneNumber,
            string passportData,
            bool adminContext = false,
            string returnTo = "")
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Login == login))
                {
                    ViewBag.Error = "Пользователь с таким логином уже существует";
                    return View();
                }

                if (await _context.Users.AnyAsync(u => u.Email == email))
                {
                    ViewBag.Error = "Пользователь с таким email уже существует";
                    return View();
                }

                if (await _context.Citizens.AnyAsync(c => c.PassportData == passportData))
                {
                    ViewBag.Error = "Пользователь с такими паспортными данными уже зарегистрирован";
                    return View();
                }

                var user = new User
                {
                    Login = login,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = phoneNumber,
                    UserType = "Citizen",
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var citizen = new Citizen
                {
                    CitizenId = user.UserId,
                    PassportData = passportData
                };

                _context.Citizens.Add(citizen);
                await _context.SaveChangesAsync();

                // ЕСЛИ регистрирует админ - НЕ логинимся как новый пользователь
                if (adminContext && returnTo == "ViewAll")
                {
                    TempData["SuccessMessage"] = $"Пользователь {user.FullName} успешно создан!";
                    return RedirectToAction("ViewAll", "Account");
                }
                // ИНАЧЕ - стандартное поведение (логинимся как новый пользователь)
                else
                {
                    HttpContext.Session.SetString("UserId", user.UserId.ToString());
                    HttpContext.Session.SetString("UserName", user.FullName);
                    HttpContext.Session.SetString("UserType", user.UserType);

                    return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ОШИБКА РЕГИСТРАЦИИ ===");
                Console.WriteLine($"Сообщение: {ex.Message}");
                Console.WriteLine($"Тип исключения: {ex.GetType()}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
                Console.WriteLine($"========================");
                ViewBag.Error = $"Произошла ошибка при регистрации";
                return View();
            }
        }

        // GET: /Account/Logout - выход из системы
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Profile - открыть страницу профиля
        public async Task<IActionResult> Profile()
        {
            // Проверяем авторизацию
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);
            var userType = HttpContext.Session.GetString("UserType");

            // Получаем базовые данные пользователя
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Передаем данные через ViewBag
            ViewBag.UserData = user;
            ViewBag.UserType = userType;

            // Для граждан получаем их объекты
            if (userType == "Citizen")
            {
                var objects = await _context.CadastralObjects
                    .Where(co => co.OwnerId == userId)
                    .OrderByDescending(co => co.RegistrationDate)
                    .ToListAsync();

                ViewBag.CadastralObjects = objects;

                // Паспортные данные
                var citizen = await _context.Citizens.FindAsync(userId);
                ViewBag.PassportData = citizen?.PassportData;
            }
            // Для сотрудников получаем отдел
            else if (userType == "Employee" || userType == "Admin")
            {
                var employee = await _context.Employees.FindAsync(userId);
                ViewBag.Department = employee?.Department;

                // Статистика системы (только для сотрудников и админов)
                var totalObjects = await _context.CadastralObjects.CountAsync();
                var totalApplications = await _context.Applications.CountAsync();
                var pendingApplications = await _context.Applications
                    .CountAsync(a => a.ApplicationStatus == "Принят к проверке" ||
                                    a.ApplicationStatus == "На проверке");
                var totalCitizens = await _context.Citizens.CountAsync();

                ViewBag.TotalObjects = totalObjects;
                ViewBag.TotalApplications = totalApplications;
                ViewBag.PendingApplications = pendingApplications;
                ViewBag.TotalCitizens = totalCitizens;
            }

            return View();
        }

        // GET: /Account/ViewAll - посмотреть всех пользователей с пагинацией и поиском
        public async Task<IActionResult> ViewAll(
            string search = "",
            string searchType = "all",
            string userTypeFilter = "all",
            int page = 1,
            int pageSize = 10)
        {
            // Проверка прав доступа - только для админа
            if (HttpContext.Session.GetString("UserType") != "Admin")
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            // Основной запрос пользователей
            IQueryable<User> query = _context.Users.AsQueryable();

            // Фильтр по типу пользователя
            if (userTypeFilter != "all" && !string.IsNullOrEmpty(userTypeFilter))
            {
                query = query.Where(u => u.UserType == userTypeFilter);
            }

            // Поиск по разным критериям
            if (!string.IsNullOrEmpty(search))
            {
                switch (searchType)
                {
                    case "fullName":
                        query = query.Where(u => u.FullName.Contains(search));
                        break;
                    case "login":
                        query = query.Where(u => u.Login.Contains(search));
                        break;
                    case "email":
                        query = query.Where(u => u.Email.Contains(search));
                        break;
                    case "phone":
                        query = query.Where(u => u.PhoneNumber.Contains(search));
                        break;
                    case "passport":
                        // Для поиска по паспорту - подзапрос к Citizens
                        var citizenIds = _context.Citizens
                            .Where(c => c.PassportData.Contains(search))
                            .Select(c => c.CitizenId);
                        query = query.Where(u => citizenIds.Contains(u.UserId));
                        break;
                    case "all":
                    default:
                        query = query.Where(u =>
                            u.FullName.Contains(search) ||
                            u.Login.Contains(search) ||
                            u.Email.Contains(search) ||
                            (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
                        break;
                }
            }

            // Подготовка данных для отображения
            var totalUsers = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

            var users = await query
                .OrderBy(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Получаем дополнительные данные для Citizen и Employee
            var viewModel = new List<UserAdminViewModel>();

            foreach (var user in users)
            {
                var userViewModel = new UserAdminViewModel
                {
                    User = user
                };

                if (user.UserType == "Citizen")
                {
                    userViewModel.Citizen = await _context.Citizens
                        .FirstOrDefaultAsync(c => c.CitizenId == user.UserId);
                }
                else if (user.UserType == "Employee" || user.UserType == "Admin")
                {
                    userViewModel.Employee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeId == user.UserId);
                }

                viewModel.Add(userViewModel);
            }

            // Передаем данные в View
            ViewBag.Users = viewModel;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.Search = search;
            ViewBag.SearchType = searchType;
            ViewBag.UserTypeFilter = userTypeFilter;

            return View();
        }

        // POST: /Account/ChangeUserType - изменение роли пользователя
        [HttpPost]
        public async Task<IActionResult> ChangeUserType(int userId, string newUserType)
        {
            if (HttpContext.Session.GetString("UserType") != "Admin")
            {
                return Json(new { success = false, message = "Доступ запрещен" });
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Пользователь не найден" });
                }

                var oldUserType = user.UserType;
                user.UserType = newUserType;

                // Если меняем на Citizen - проверяем наличие записи в Citizens
                if (newUserType == "Citizen")
                {
                    var citizen = await _context.Citizens.FindAsync(userId);
                    if (citizen == null)
                    {
                        // Создаем запись с пустыми паспортными данными
                        _context.Citizens.Add(new Citizen
                        {
                            CitizenId = userId,
                            PassportData = "0000000000"
                        });
                    }

                    // УДАЛЯЕМ запись из Employees если существует
                    var employee = await _context.Employees.FindAsync(userId);
                    if (employee != null)
                    {
                        _context.Employees.Remove(employee);
                    }
                }
                // Если меняем на Employee/Admin - проверяем наличие записи в Employees
                else if (newUserType == "Employee" || newUserType == "Admin")
                {
                    var employee = await _context.Employees.FindAsync(userId);
                    if (employee == null)
                    {
                        _context.Employees.Add(new Employee
                        {
                            EmployeeId = userId,
                            Department = "Отдел: - "
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Роль успешно изменена" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        // POST: /Account/ChangeDepartment - изменение отдела сотрудника
        [HttpPost]
        public async Task<IActionResult> ChangeDepartment(int userId, string newDepartment)
        {
            if (HttpContext.Session.GetString("UserType") != "Admin")
            {
                return Json(new { success = false, message = "Доступ запрещен" });
            }

            try
            {
                var employee = await _context.Employees.FindAsync(userId);
                if (employee == null)
                {
                    // Проверяем, может пользователь еще не сотрудник
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                    {
                        return Json(new { success = false, message = "Пользователь не найден" });
                    }

                    // Создаем запись сотрудника
                    employee = new Employee
                    {
                        EmployeeId = userId,
                        Department = newDepartment
                    };
                    _context.Employees.Add(employee);
                }
                else
                {
                    employee.Department = newDepartment;
                    _context.Update(employee);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Отдел успешно изменен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        // POST: /Account/DeleteUser - удаление пользователя
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            if (HttpContext.Session.GetString("UserType") != "Admin")
            {
                return Json(new { success = false, message = "Доступ запрещен" });
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Пользователь не найден" });
                }

                // Проверяем, нет ли связанных данных
                var hasCadastralObjects = await _context.CadastralObjects
                    .AnyAsync(co => co.OwnerId == userId);

                var hasApplications = await _context.Applications
                    .AnyAsync(a => a.ApplicantId == userId);

                if (hasCadastralObjects || hasApplications)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Нельзя удалить пользователя, у которого есть связанные данные (объекты, заявления)"
                    });
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Пользователь успешно удален" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
        }

        // ViewModel для отображения
        public class UserAdminViewModel
        {
            public User User { get; set; }
            public Citizen Citizen { get; set; }
            public Employee Employee { get; set; }
        }
    }
}