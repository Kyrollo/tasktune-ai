using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TaskTune.API.Support;

public static class AgentServiceSecurity
{
    private const string HeaderName = "X-TaskTune-Service-Key";

    public static bool TryConfigure(HttpClient client, IConfiguration configuration)
    {
        var key = configuration["AgentServices:ServiceApiKey"];
        if (string.IsNullOrWhiteSpace(key) ||
            Encoding.UTF8.GetByteCount(key) < 32 ||
            key.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            return false;
        }

        client.DefaultRequestHeaders.Remove(HeaderName);
        return client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderName, key);
    }
}
