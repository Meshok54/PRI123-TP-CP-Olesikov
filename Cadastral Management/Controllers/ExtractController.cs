using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cadastral_Management.Controllers
{
    public class ExtractController : Controller
    {
        // GET: /Extract/Request - запросить выписку
        public ActionResult MyExtracts()
        {
            return View();
        }
    }
}
