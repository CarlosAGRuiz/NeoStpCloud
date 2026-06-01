using Microsoft.AspNetCore.Authentication.Cookies;
using NeoSTP.Application;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Legal;
using NeoSTP.Infrastructure;
using NeoSTP.Web.Auth;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<LegalOptions>(builder.Configuration.GetSection("Legal"));

builder.Services.AddScoped<ICurrentUser, CookieCurrentUser>();
builder.Services.AddScoped<NeoSTP.Application.Empresas.IEmpresaContext, NeoSTP.Web.Auth.WebEmpresaContext>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "NeoStp.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

try
{
    Log.Information("Starting NeoSTP.Web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NeoSTP.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
