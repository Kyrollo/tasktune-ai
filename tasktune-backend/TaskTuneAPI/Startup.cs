using Microsoft.Owin;
using Owin;
using System.Web.Http;
using System.Web.Http.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.Http.Formatting;

[assembly: OwinStartup(typeof(TaskTuneAPI.Startup))]

namespace TaskTuneAPI
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var config = new HttpConfiguration();

            // ── CORS — allow same origin and localhost ──
            var cors = new EnableCorsAttribute(
                origins: "https://localhost:44352,http://localhost:44352,http://localhost:5173,http://localhost:5174,http://localhost:3000",
                headers: "*",
                methods: "*"
            );
            config.EnableCors(cors);

            // Attribute routing
            config.MapHttpAttributeRoutes();

            // Convention-based routing
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // JSON formatting
            config.Formatters.Clear();
            config.Formatters.Add(new JsonMediaTypeFormatter
            {
                SerializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                }
            });

            WebApiConfig.Register(config);

            app.UseWebApi(config);
        }
    }
}