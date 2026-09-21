namespace KontentMvc.Web.Content;

/// <summary>
/// Language codenames of the Kontent.ai environment, taken from its /languages endpoint.
/// The environment has two languages, so every Delivery query states which one it wants:
/// a query without a language silently serves the environment's default.
/// </summary>
public static class ContentLanguages
{
    /// <summary>English (en-US); the environment's default language, codename "default".</summary>
    public const string Default = "default";

    /// <summary>Spanish (es-ES). No route serves it yet; see the report for what a second language would need.</summary>
    public const string Spanish = "es-ES";
}
