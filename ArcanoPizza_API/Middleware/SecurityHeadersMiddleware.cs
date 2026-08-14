namespace ArcanoPizza_API.Middleware;

/// <summary>
/// Middleware para añadir cabeceras de seguridad (OWASP A02: Security Misconfiguration).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevenir clickjacking
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // Evitar MIME sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Política de referrer restringida
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Política de permisos (deshabilita features no usadas)
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

        // Swagger UI necesita scripts/estilos inline; el resto de la API sigue con CSP estricto.
        var path = context.Request.Path;
        var isSwagger = path.StartsWithSegments("/swagger") || path.StartsWithSegments("/openapi");
        var csp = isSwagger
            ? "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'"
            : "default-src 'self'; frame-ancestors 'none'";
        context.Response.Headers.Append("Content-Security-Policy", csp);

        await _next(context);
    }
}
