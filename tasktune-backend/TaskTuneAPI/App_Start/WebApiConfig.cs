using System.Net.Http.Formatting;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TaskTuneAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
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

            // Optional: enable CORS
            // config.EnableCors(new EnableCorsAttribute("*", "*", "*"));
        }
    }
}