using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

using OEEMonitoringSystem.Models.ViewModel; // <-- make sure you have ErrorViewModel here

namespace OEEMonitoringSystem.Controllers
{
    [AllowAnonymous] // anyone can access error pages
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // 🔹 Handle status codes like 404, 403, etc.
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            var statusCodeResult = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

            switch (statusCode)
            {
                case 404:
                    ViewBag.ErrorMessage = "Sorry, the page you requested could not be found.";
                    _logger.LogWarning("404 Error: Path={Path} | QueryString={Query}",
                        statusCodeResult?.OriginalPath,
                        statusCodeResult?.OriginalQueryString);
                    return View("NotFound");

                case 403:
                    ViewBag.ErrorMessage = "You are not authorized to access this resource.";
                    _logger.LogWarning("403 Forbidden: Path={Path}", statusCodeResult?.OriginalPath);
                    return View("~/Views/Account/AccessDenied.cshtml");

                default:
                    ViewBag.ErrorMessage = "An unexpected error occurred.";
                    _logger.LogError("Unexpected status code: {StatusCode} | Path={Path}",
                        statusCode,
                        statusCodeResult?.OriginalPath);
                    return View("ServerError");
            }
        }

        // 🔹 Handle unhandled exceptions (500)
        [Route("Error")]
        public IActionResult Error()
        {
            var exceptionDetails = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionDetails != null)
            {
                _logger.LogError(exceptionDetails.Error,
                    "Unhandled exception at path {Path}", exceptionDetails.Path);

                ViewBag.Path = exceptionDetails.Path;
                ViewBag.Error = exceptionDetails.Error.Message;
            }

            ViewBag.ErrorMessage = "A server error occurred. Please try again later.";

            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };

            return View("ServerError", model);
        }
    }
}
