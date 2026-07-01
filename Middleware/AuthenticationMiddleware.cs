namespace Cafe.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationMiddleware> _logger;

        public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();

            var publicPaths = new[] { "/auth/login", "/auth/register", "/auth/accessdenied", "/onboarding" };
            var isPublicPath = path == "/" ||
                              publicPaths.Any(p => path?.StartsWith(p) == true) ||
                              path?.Contains("/css/") == true || path?.Contains("/js/") == true ||
                              path?.Contains("/images/") == true ||
                              path?.StartsWith("/hubs/") == true ||
                              // Payment terminal webhooks are server-to-server (no session);
                              // they authenticate with a shared secret inside the controller.
                              path?.StartsWith("/paymentwebhook") == true;

            if (!isPublicPath)
            {
                var session = context.Session;
                if (!session.Keys.Contains("UserId"))
                {
                    _logger.LogWarning("Unauthorized access attempt to {Path}", path);
                    context.Response.Redirect("/Auth/Login");
                    return;
                }
            }

            await _next(context);
        }
    }
}
