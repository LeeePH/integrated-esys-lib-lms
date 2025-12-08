using E_SysV0._01.Hubs;
using E_SysV0._01.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Server.Kestrel.Core;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(); builder.Services.AddAuthorization();
builder.Services.AddSignalR();
// Request size limits for file uploads
builder.Services.Configure<IISServerOptions>(options => { options.MaxRequestBodySize = 50 * 1024 * 1024; });
builder.Services.Configure<KestrelServerOptions>(options => { options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; });
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<MongoDBServices>();
builder.Services.AddSingleton<EmailServices>();
builder.Services.AddSingleton<RegistrationSlipPdfService>();
builder.Services.AddSingleton<EnrollmentCycleService>(); // NEW
builder.Services.AddScoped<RegistrationSlipPdfService>();
builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = "AppAuth";
    options.DefaultChallengeScheme = "AppAuth";
}).AddPolicyScheme("AppAuth", "AppAuth",
    options => {
        options.ForwardDefaultSelector = context => {
            var path = context.Request.Path; if (path.StartsWithSegments("/Admin")) return "AdminCookie"; if (path.StartsWithSegments("/Student")) return "StudentCookie";
            var cookies = context.Request.Cookies;
            if (cookies.ContainsKey(".E-Sys.Admin"))
                return "AdminCookie";
            if (cookies.ContainsKey(".E-Sys.Student"))
                return "StudentCookie";
            return "StudentCookie";
        };
    })
.AddCookie("AdminCookie", options =>
{
    options.LoginPath = "/Admin/Admin/AdminLogin";
    options.AccessDeniedPath = "/Admin/Admin/AdminLogin";
    options.Cookie.Name = ".E-Sys.Admin";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
})
.AddCookie("StudentCookie", options =>
{
    options.LoginPath = "/Student/StudentAccount/StudentLogin";
    options.AccessDeniedPath = "/Student/StudentAccount/StudentLogin";
    options.Cookie.Name = ".E-Sys.Student";
});
try
{
    var app = builder.Build();
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
    }

    // Simple DB health probe
    app.MapGet("/health/db", async (MongoDBServices svc) =>
    {
        var ok = await svc.CanConnectAsync();
        return ok ? Results.Ok(new { ok }) : Results.Problem("MongoDB connection failed");
    });

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHub<AdminNotificationsHub>("/hubs/admin");

    // Landing page -> Student login
    app.MapControllerRoute(
        name: "root",
        pattern: "",
        defaults: new { area = "Student", controller = "StudentAccount", action = "StudentLogin" });

    // Convenience: /Student -> Student login
    app.MapControllerRoute(
        name: "student_root",
        pattern: "Student",
        defaults: new { area = "Student", controller = "StudentAccount", action = "StudentLogin" });



    // Areas and default MVC
    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapGet("/kiosk/freshmen", ctx =>
    {
        ctx.Response.Redirect("/Student/FreshmenEnrollment/FreshmenEnrollment", permanent: false);
        return Task.CompletedTask;
    });

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    using (var scope = app.Services.CreateScope())
    {
        var mongo = scope.ServiceProvider.GetRequiredService<MongoDBServices>();

        // Ensure settings singleton exists
        await mongo.GetEnrollmentSettingsAsync();

        // Optional: initial scheduling
        await mongo.SeedSchedulingDataIfEmptyAsync(year: DateTime.UtcNow.Year, program: "BSIT", sectionCapacity: 1);
    }

    app.Run();
}
catch (Exception ex) { Console.Error.WriteLine($"Fatal host error: {ex}"); throw; }