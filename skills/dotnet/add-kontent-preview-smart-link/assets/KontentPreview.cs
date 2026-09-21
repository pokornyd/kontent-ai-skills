// Preview mode for a server-rendered ASP.NET Core app on Kontent.ai.
// Copy into the web project (for example Preview/KontentPreview.cs) and change the namespace.
// Wire-up: builder.Services.AddKontentPreview(builder.Configuration);  app.UseKontentPreview();
using System.Security.Cryptography;
using System.Text;
using Kontent.Ai.Delivery;
using Kontent.Ai.Delivery.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace YourApp.Preview;

public sealed class PreviewOptions
{
    public const string SectionName = "PreviewOptions";

    /// <summary>Shared secret carried by the preview URLs configured in Kontent.ai. User secrets or an
    /// environment variable (PreviewOptions__Secret), never a tracked file. Empty means preview is off.</summary>
    public string? Secret { get; set; }

    public string SecretQueryParameter { get; set; } = "secret";
    public string CookieName { get; set; } = ".Kontent.Preview";
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);
    public string ExitPath { get; set; } = "/preview/exit";

    /// <summary>Who may frame previewed pages. Kontent.ai's live preview loads the site in an iframe.</summary>
    public string FrameAncestors { get; set; } = "https://app.kontent.ai";
}

/// <summary>Whether the current request carries a valid preview grant. Scoped; set by the middleware.</summary>
public interface IPreviewContext
{
    bool IsPreview { get; }
}

public sealed class PreviewContext : IPreviewContext
{
    public bool IsPreview { get; set; }
}

/// <summary>Issues and checks the cookie value. It is protected and time-limited, so a visitor cannot
/// type the cookie into devtools, and a stolen one stops working after <see cref="PreviewOptions.Lifetime"/>.</summary>
public sealed class PreviewTokenProtector(IDataProtectionProvider provider)
{
    // Changing the purpose string invalidates every preview cookie already issued.
    private const string Payload = "preview";
    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("Kontent.Preview.v1").ToTimeLimitedDataProtector();

    public string Issue(TimeSpan lifetime) => _protector.Protect(Payload, lifetime);

    public bool IsValid(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            return _protector.Unprotect(token) == Payload;
        }
        catch (CryptographicException)
        {
            return false; // forged, expired, or protected by a key ring this instance does not have
        }
    }
}

public sealed class PreviewMiddleware(
    RequestDelegate next,
    IOptions<PreviewOptions> options,
    PreviewTokenProtector protector,
    ILogger<PreviewMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, PreviewContext preview)
    {
        var o = options.Value;
        var request = context.Request;

        if (request.Path.Equals(o.ExitPath, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Cookies.Delete(o.CookieName, CookieOptions(o, expires: false));
            context.Response.Redirect(LocalOrRoot(request.Query["returnUrl"]));
            return;
        }

        if (request.Query.TryGetValue(o.SecretQueryParameter, out var supplied))
        {
            if (!SecretMatches(supplied.ToString(), o.Secret))
            {
                // Answer as if the page did not exist: a prober learns nothing, and an editor with a stale
                // preview URL gets an obvious failure instead of silently seeing published content.
                logger.LogWarning("Rejected a preview request for {Path}: secret missing, wrong, or preview is not configured.", request.Path);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.Cookies.Append(o.CookieName, protector.Issue(o.Lifetime), CookieOptions(o, expires: true));

            // Back to the same URL without the secret, so it never reaches rendered links, the address bar
            // inside the editor, referrers or logs further down the pipeline.
            var query = QueryString.Create(request.Query
                .Where(pair => !string.Equals(pair.Key, o.SecretQueryParameter, StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value.Select(value => KeyValuePair.Create(pair.Key, value))));
            context.Response.Redirect(LocalOrRoot(request.PathBase + request.Path) + query);
            return;
        }

        preview.IsPreview = !string.IsNullOrEmpty(o.Secret) && protector.IsValid(request.Cookies[o.CookieName]);

        if (preview.IsPreview)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.CacheControl = "no-store";              // drafts must not reach a shared cache
                headers["X-Robots-Tag"] = "noindex, nofollow";
                headers.Remove("X-Frame-Options");              // antiforgery adds SAMEORIGIN, which blocks the iframe
                var csp = headers.ContentSecurityPolicy.ToString();
                if (!csp.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase))
                {
                    headers.ContentSecurityPolicy = (csp.Length == 0 ? string.Empty : csp.TrimEnd(';', ' ') + "; ")
                        + "frame-ancestors " + o.FrameAncestors;
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    // SameSite=None and Secure are what let the cookie travel inside Kontent.ai's cross-site iframe.
    // The ASP.NET Core default, Lax, works in a browser tab and silently fails in live preview.
    private static CookieOptions CookieOptions(PreviewOptions o, bool expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        IsEssential = true,
        Path = "/",
        MaxAge = expires ? o.Lifetime : null,
    };

    private static bool SecretMatches(string supplied, string? expected) =>
        !string.IsNullOrEmpty(supplied)
        && !string.IsNullOrEmpty(expected)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));

    // Only same-site paths: never "//host", "/\host" or anything absolute.
    private static string LocalOrRoot(string? path) =>
        !string.IsNullOrEmpty(path) && path[0] == '/' && (path.Length == 1 || (path[1] != '/' && path[1] != '\\'))
        && !path.Any(char.IsControl)
            ? path
            : "/";
}

public static class KontentPreviewExtensions
{
    public const string ClientName = "preview";

    public static IServiceCollection AddKontentPreview(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PreviewOptions>().Bind(configuration.GetSection(PreviewOptions.SectionName));
        services.AddDataProtection();
        services.AddSingleton<PreviewTokenProtector>();
        services.AddScoped<PreviewContext>();
        services.AddScoped<IPreviewContext>(provider => provider.GetRequiredService<PreviewContext>());

        // A second, named client on the Preview API. The default client stays exactly as it was, so
        // visitors are served by the same registration as before. Without a key it is not registered
        // and ForRequest falls back to published content.
        var section = DeliveryOptions.DefaultConfigurationSectionName;
        if (!string.IsNullOrWhiteSpace(configuration[$"{section}:{nameof(DeliveryOptions.PreviewApiKey)}"]))
        {
            services.AddDeliveryClient(ClientName, delivery => delivery.Options
                .BindConfiguration(section)
                .Configure(o =>
                {
                    o.UseSecureAccess = false; // mutually exclusive with the Preview API; options validation throws on both
                    o.UsePreviewApi(o.PreviewApiKey!);
                }));
        }

        return services;
    }

    /// <summary>Place before the endpoints, after UseHttpsRedirection.</summary>
    public static IApplicationBuilder UseKontentPreview(this IApplicationBuilder app) =>
        app.UseMiddleware<PreviewMiddleware>();

    /// <summary>The client this request's queries run on: preview for a valid grant, the default otherwise.</summary>
    public static IDeliveryClient ForRequest(this IDeliveryClientFactory factory, IPreviewContext preview) =>
        preview.IsPreview ? factory.TryGet(ClientName) ?? factory.Get() : factory.Get();
}
