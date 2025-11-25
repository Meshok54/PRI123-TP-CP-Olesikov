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
                // Ищу пользователя по логину
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Login == login);

                if (user == null)
                {
                    ViewBag.Error = "Пользователь с таким логином не найден";
                    return View();
                }

                // Проверяю пароль с помощью BCrypt
                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    ViewBag.Error = "Неверный пароль";
                    return View();
                }

                // Сохраняю данные в сессии
                HttpContext.Session.SetString("UserId", user.UserId.ToString());
                HttpContext.Session.SetString("UserName", user.FullName);
                HttpContext.Session.SetString("UserType", user.UserType);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                // Логируем ошибку
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
            string passportData)
        {
            try
            {
                // Проверяю уникальность логина и email
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

                // Проверяю уникальность паспортных данных
                if (await _context.Citizens.AnyAsync(c => c.PassportData == passportData))
                {
                    ViewBag.Error = "Пользователь с такими паспортными данными уже зарегистрирован";
                    return View();
                }

                // Создаю запись в Users
                var user = new User
                {
                    Login = login,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), // Хэширую пароль
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = phoneNumber,
                    UserType = "Citizen",
                    CreatedAt = DateTime.Now // created_at заполнится автоматически, но на всякий случай
                };

                // Сохраняю пользователя чтобы получить UserId
                _context.Users.Add(user);
                await _context.SaveChangesAsync(); // Здесь генерирую UserId

                // Создаю запись в Citizens с тем же ID
                var citizen = new Citizen
                {
                    CitizenId = user.UserId,
                    PassportData = passportData
                };

                _context.Citizens.Add(citizen);
                await _context.SaveChangesAsync();

                // Автоматически логиню пользователя после регистрации
                HttpContext.Session.SetString("UserId", user.UserId.ToString());
                HttpContext.Session.SetString("UserName", user.FullName);
                HttpContext.Session.SetString("UserType", user.UserType);

                return RedirectToAction("Index", "Home");
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
    }
}