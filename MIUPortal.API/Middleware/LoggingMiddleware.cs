namespace MIUPortal.API.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;

        public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)

        {
            if (context.Request.Path.StartsWithSegments("/.well-known"))
            {
                await _next(context);
                return;
            }
            var startTime = DateTime.UtcNow;
            var request = context.Request;

            _logger.LogInformation($"[{request.Method}] {request.Path} - Started at {startTime:yyyy-MM-dd HH:mm:ss}");

            // Call the next middleware
            await _next(context);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation($"[{request.Method}] {request.Path} - Completed with status {context.Response.StatusCode} in {duration.TotalMilliseconds}ms");
        }
    }
}

