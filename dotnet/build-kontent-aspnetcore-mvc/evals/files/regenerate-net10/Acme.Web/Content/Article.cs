namespace Acme.Web.Content;

public partial record Article
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Untitled article" : Title;
}
