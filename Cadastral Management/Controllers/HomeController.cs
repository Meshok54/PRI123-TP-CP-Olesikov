using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cadastral_Management.Data;
using Cadastral_Management.Models;


namespace Cadastral_Management.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
                    string search = "",
                    string searchType = "cadastralNumber",
                    int page = 1,
                    int pageSize = 10)
        {
            // Собираем данные для таблицы (только для Admin/Employee)
            if (HttpContext.Session.GetString("UserType") == "Admin" ||
                HttpContext.Session.GetString("UserType") == "Employee")
            {
                IQueryable<CadastralObject> query = _context.CadastralObjects
                    .Include(co => co.Owner)
                    .ThenInclude(o => o.User);

                // Поиск по разным критериям
                if (!string.IsNullOrEmpty(search))
                {
                    switch (searchType)
                    {
                        case "cadastralNumber":
                            query = query.Where(co => co.CadastralNumber.Contains(search));
                            break;
                        case "address":
                            query = query.Where(co => co.Address.Contains(search));
                            break;
                        case "owner":
                            query = query.Where(co =>
                                co.Owner.User.FullName.Contains(search) ||
                                co.Owner.User.Login.Contains(search));
                            break;
                    }
                }

                // Пагинация
                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var cadastralObjects = await query
                    .OrderByDescending(co => co.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.CadastralObjects = cadastralObjects;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.Search = search;
                ViewBag.SearchType = searchType;
            }

            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}