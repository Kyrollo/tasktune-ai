using System.Web.Mvc;
using System.Web.Routing;

namespace TaskTuneAPI
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            // Block direct access to view files
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{*allcshtml}", new { allcshtml = @".*\.cshtml(/.*)?" });

            // MVC named routes
            routes.MapRoute(
                name: "ApiDetail",
                url: "apis/detail",
                defaults: new { controller = "Home", action = "Detail" }
            );

            routes.MapRoute(
                name: "ApiList",
                url: "apis",
                defaults: new { controller = "Home", action = "Apis" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}