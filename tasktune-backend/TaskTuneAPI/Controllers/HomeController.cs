using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;

namespace TaskTuneAPI.Controllers
{
    public class HomeController : Controller
    {
        private const string BaseUrl = "https://localhost";

        // GET /
        public ActionResult Index()
        {
            return View();
        }

        // GET /apis
        public ActionResult Apis()
        {
            var routes = GetAllApiRoutes();
            return View(routes);
        }

        // GET /apis/detail?path=api/...
        public ActionResult Detail(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return RedirectToAction("Apis");

            var routes = GetAllApiRoutes();
            var route = routes.FirstOrDefault(r =>
                r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (route == null) return HttpNotFound();

            ViewBag.Route = route;
            return View();
        }

        // ── Auto-discovery via Reflection ──────────────────────────────────
        public static List<ApiRouteInfo> GetAllApiRoutes()
        {
            var results = new List<ApiRouteInfo>();
            var assembly = Assembly.GetExecutingAssembly();

            // Use fully qualified names to avoid ambiguity between
            // System.Web.Mvc and System.Web.Http attribute types
            var apiControllerType = typeof(System.Web.Http.ApiController);
            var routePrefixAttrType = typeof(System.Web.Http.RoutePrefixAttribute);
            var routeAttrType = typeof(System.Web.Http.RouteAttribute);
            var httpPostAttrType = typeof(System.Web.Http.HttpPostAttribute);
            var httpPutAttrType = typeof(System.Web.Http.HttpPutAttribute);
            var httpDeleteAttrType = typeof(System.Web.Http.HttpDeleteAttribute);
            var httpPatchAttrType = typeof(System.Web.Http.HttpPatchAttribute);

            var apiControllers = assembly.GetTypes()
                .Where(t => apiControllerType.IsAssignableFrom(t) && !t.IsAbstract)
                .OrderBy(t => t.Name);

            foreach (var controller in apiControllers)
            {
                string controllerName = controller.Name.Replace("Controller", "").ToLower();

                // Detect [RoutePrefix] — fully qualified to System.Web.Http
                var routePrefix = controller
                    .GetCustomAttributes(routePrefixAttrType, true)
                    .FirstOrDefault() as System.Web.Http.RoutePrefixAttribute;
                string prefix = routePrefix?.Prefix;

                var methods = controller
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName);

                foreach (var method in methods)
                {
                    // Detect [Route] — fully qualified to System.Web.Http
                    var routeAttr = method
                        .GetCustomAttributes(routeAttrType, true)
                        .FirstOrDefault() as System.Web.Http.RouteAttribute;
                    string routePath = routeAttr?.Template;

                    // Detect HTTP verb — all fully qualified to System.Web.Http
                    string verb = "GET";
                    if (method.GetCustomAttributes(httpPostAttrType, true).Any()) verb = "POST";
                    else if (method.GetCustomAttributes(httpPutAttrType, true).Any()) verb = "PUT";
                    else if (method.GetCustomAttributes(httpDeleteAttrType, true).Any()) verb = "DELETE";
                    else if (method.GetCustomAttributes(httpPatchAttrType, true).Any()) verb = "PATCH";

                    string fullPath;
                    if (routePath != null)
                        fullPath = routePath.StartsWith("api/") ? routePath
                                   : (prefix != null ? prefix + "/" + routePath
                                                     : "api/" + controllerName + "/" + routePath);
                    else
                        fullPath = prefix ?? ("api/" + controllerName);

                    results.Add(new ApiRouteInfo
                    {
                        ControllerName = controller.Name.Replace("Controller", ""),
                        ActionName = method.Name,
                        Verb = verb,
                        Path = fullPath.TrimStart('/'),
                        FullUrl = BaseUrl + "/" + fullPath.TrimStart('/')
                    });
                }
            }

            return results.OrderBy(r => r.Path).ThenBy(r => r.Verb).ToList();
        }
    }

    // ── DTO ────────────────────────────────────────────────────────────────
    public class ApiRouteInfo
    {
        public string ControllerName { get; set; }
        public string ActionName { get; set; }
        public string Verb { get; set; }
        public string Path { get; set; }
        public string FullUrl { get; set; }
    }
}
