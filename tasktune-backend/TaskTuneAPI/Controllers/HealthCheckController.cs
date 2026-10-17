using System;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Models;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/health")]
    public class HealthCheckController : ApiController
    {
        [HttpGet, Route("")]
        public IHttpActionResult Ping()
        {
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        [HttpGet, Route("db")]
        public async Task<IHttpActionResult> CheckDatabase()
        {
            try
            {
                using (var db = new TaskTuneDbContext())
                {
                    // Try to open a connection and run a simple query
                    var canConnect = db.Database.Connection.State == System.Data.ConnectionState.Open
                        || db.Database.Exists();

                    if (!canConnect)
                    {
                        db.Database.Connection.Open();
                        db.Database.Connection.Close();
                    }

                    // Try a real query
                    var companyCount = await System.Data.Entity.QueryableExtensions
                        .CountAsync(db.Companies);

                    return Ok(new
                    {
                        status = "Connected",
                        database = db.Database.Connection.Database,
                        server = db.Database.Connection.DataSource,
                        companyCount = companyCount,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                return Content(System.Net.HttpStatusCode.ServiceUnavailable, new
                {
                    status = "Disconnected",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}