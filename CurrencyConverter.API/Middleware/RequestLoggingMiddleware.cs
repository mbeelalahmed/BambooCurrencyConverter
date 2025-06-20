using System.Diagnostics;

namespace CurrencyConverter.API.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            await _next(context);

            stopwatch.Stop();

            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            var method = context.Request.Method;
            var path = context.Request.Path;
            var statusCode = context.Response.StatusCode;
            var responseTime = stopwatch.ElapsedMilliseconds;

            string clientId = context.User?.Claims?.FirstOrDefault(c => c.Type == "client_id")?.Value;

            _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms | Client IP: {IP} | ClientId: {ClientId}",
                method, path, statusCode, responseTime, clientIp, clientId);
        }
    }

}
