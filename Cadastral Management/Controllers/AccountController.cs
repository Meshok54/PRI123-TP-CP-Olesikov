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
            string passportData)
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

        // GET: /Account/Profile - выход из системы
        public IActionResult Profile()
        {
            return View();
        }

        // GET: /Account/ViewAll - посмотреть всех пользователь
        public IActionResult ViewAll()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }
    }
}