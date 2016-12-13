using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;

namespace SampleApplication.SimpleInjector.Controllers
{
    public class HomeController : Controller
    {
        private readonly Func<IViewBufferScope> scopeProvider;

        public HomeController(Func<IViewBufferScope> scopeProvider)
        {
            this.scopeProvider = scopeProvider;
        }

        public IActionResult Index()
        {
            var scope = this.scopeProvider();
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}