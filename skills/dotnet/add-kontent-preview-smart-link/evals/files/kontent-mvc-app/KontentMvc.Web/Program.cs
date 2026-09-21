using System.Text.Encodings.Web;
using Kontent.Ai.AspNetCore.ImageTransformation;
using Kontent.Ai.AspNetCore.RichText;
using Kontent.Ai.Delivery;
using KontentMvc.Web.Content;
using KontentMvc.Web.Models.Mappers;
using KontentMvc.Web.Services.Content;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Kontent.ai Delivery client. Preview and Secure Access keys belong in user secrets or in
// environment variables such as DeliveryOptions__SecureAccessApiKey, never in tracked files.
builder.Services.AddDeliveryClient(delivery => delivery.Options.BindConfiguration("DeliveryOptions"));

// Rich-text resolvers. The rich-text inventory of the "article" type reported one embedded
// component type, "disclaimer"; with no resolver the SDK renders a "Missing resolver" HTML comment
// in its place. Resolver output is inserted unescaped, so editor-controlled text is encoded here.
builder.Services.AddKontentRichText(resolvers => resolvers
    .WithContentResolver<Disclaimer>(disclaimer =>
    {
        var elements = disclaimer.Elements;
        var headline = HtmlEncoder.Default.Encode(elements.Headline ?? string.Empty);
        var subheadline = HtmlEncoder.Default.Encode(elements.Subheadline ?? string.Empty);

        return $"<aside class=\"alert alert-secondary disclaimer\" role=\"note\">" +
               $"<strong class=\"d-block\">{headline}</strong>{subheadline}</aside>";
    }));

// The width ladder the <img-asset> tag helper builds srcset from.
builder.Services.Configure<ImageTransformationOptions>(
    builder.Configuration.GetSection(nameof(ImageTransformationOptions)));

builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<PersonMapper>();
builder.Services.AddScoped<ArticleMapper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

// SDK workaround, not a preference: on this .NET 10 SDK build (10.0.401) MapStaticAssets answers a
// gzip-encoded request with 200 and an empty body, so every browser gets a zero-byte site.css. A
// throwaway `dotnet new mvc` shows the same, so it is not this app. Restore MapStaticAssets() (and
// .WithStaticAssets() below) once an SDK that serves compressed static assets correctly is in use.
app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
