using System.Globalization;
using Scenarios.Blazor.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
// La culture suit la langue du navigateur (celle du système sinon) : le circuit en français écrit des virgules décimales.
var system = CultureInfo.CurrentCulture.Name is { Length: > 0 } name ? name : "en-US";
// Sans les réglages de l'utilisateur : un Windows en français peut garder le point décimal, le test ne prouverait rien.
var localization = new RequestLocalizationOptions { CultureInfoUseUserOverride = false }.SetDefaultCulture(system)
    .AddSupportedCultures(system, "fr-FR").AddSupportedUICultures(system, "fr-FR");
app.UseRequestLocalization(localization);
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddAdditionalAssemblies(typeof(Scenarios.Blazor.SchemaEditor).Assembly);
app.Run();
