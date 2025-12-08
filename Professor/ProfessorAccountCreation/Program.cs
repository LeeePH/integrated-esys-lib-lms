using ProfessorAccountCreation.Models;

var builder = WebApplication.CreateBuilder(args);

// ----------------------
// Register services here
// ----------------------

// Add MVC controllers
builder.Services.AddControllersWithViews();

// Bind MongoDB settings from appsettings.json
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Register MongoDbContext as singleton
builder.Services.AddSingleton<MongoDbContext>();

// Register IHttpClientFactory for API calls
builder.Services.AddHttpClient(); // <-- ADD THIS

// ----------------------
// Add session services
// ----------------------
builder.Services.AddDistributedMemoryCache(); // Required for session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ----------------------
// Configure middleware
// ----------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ----------------------
// Add session middleware here
// ----------------------
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=SuperAdmin}/{action=Index}/{id?}");

// ----------------------
// Seed default SuperAdmin
// ----------------------
using (var scope = app.Services.CreateScope())
{
    var provider = scope.ServiceProvider;
    try
    {
        var db = provider.GetRequiredService<MongoDbContext>();
        DbSeeder.SeedSuperAdmin(db);
    }
    catch (Exception ex)
    {
        // optional: log the error so startup continues
        var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger?.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();
