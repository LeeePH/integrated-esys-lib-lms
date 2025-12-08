using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Driver;
using SystemLibrary.Services;
using CloudinaryDotNet;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add CORS support for Enrollment System integration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowEnrollmentSystem", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5130",
                "https://localhost:5130",
                "http://localhost:27529",
                "https://localhost:27529",
                "http://localhost:5000",
                "https://localhost:5000",
                "http://localhost:7000",
                "https://localhost:7000"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/Login";
    options.LoginPath = "/Account/Login";
});

// Register MongoClient first — DI needs this to inject into services like StudentProfileService
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("MongoDB");
    return new MongoClient(connectionString);
});

// Register your MongoDbService which uses IMongoClient internally
builder.Services.AddSingleton<IMongoDbService, MongoDbService>();

// Register the IMongoDatabase from your MongoDbService
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var mongoDbService = sp.GetRequiredService<IMongoDbService>();
    return mongoDbService.Database;
});

// Your other services registrations
builder.Services.AddSingleton<IAuditLogService, AuditLogService>();
builder.Services.AddSingleton<IAuditLoggingHelper, AuditLoggingHelper>();
builder.Services.AddHttpContextAccessor();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddSingleton<IReportService, ReportService>();
builder.Services.AddSingleton<IReservationService, ReservationService>();
builder.Services.AddSingleton<IReturnService, ReturnService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();
builder.Services.AddSingleton<IUserManagementService, UserManagementService>();
builder.Services.AddSingleton<IBackupService, BackupService>();
builder.Services.AddSingleton<IStudentProfileService, StudentProfileService>();
builder.Services.AddSingleton<IPenaltyService, PenaltyService>();
builder.Services.AddSingleton<IUnrestrictRequestService, UnrestrictRequestService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IAuthorService, AuthorService>();
builder.Services.AddSingleton<ISubjectService, SubjectService>();
builder.Services.AddSingleton<IPublisherService, PublisherService>();
builder.Services.AddSingleton<IBookImportService, BookImportService>();
builder.Services.AddSingleton<IBookCopyService, BookCopyService>();
builder.Services.AddSingleton<AdminUserSeeder>();
builder.Services.AddSingleton<IEnrollmentSystemService, EnrollmentSystemService>();
// MOCK data service removed - now using enrollment system integration
// builder.Services.AddSingleton<IMOCKDataService, MOCKDataService>();

// Register Cloudinary (for book image storage)
var cloudName = builder.Configuration["Cloudinary:CloudName"];
var cloudApiKey = builder.Configuration["Cloudinary:ApiKey"];
var cloudApiSecret = builder.Configuration["Cloudinary:ApiSecret"];
if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(cloudApiKey) && !string.IsNullOrEmpty(cloudApiSecret))
{
    builder.Services.AddSingleton(new Cloudinary(new Account(cloudName, cloudApiKey, cloudApiSecret)));
}

// Background services
builder.Services.AddHostedService<SystemLibrary.Services.ReservationQueueProcessorService>();
builder.Services.AddHostedService<SystemLibrary.Services.OverdueProcessorService>();

// Authentication configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configure static files with custom file provider for shared uploads if configured
var uploadPath = builder.Configuration["FileUpload:UploadPath"];
if (!string.IsNullOrEmpty(uploadPath) && Path.IsPathRooted(uploadPath))
{
    // If a network/shared path is configured, serve files from there
    var sharedUploadPath = Path.Combine(uploadPath, "books");
    if (Directory.Exists(sharedUploadPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(sharedUploadPath),
            RequestPath = "/uploads/books"
        });
    }
}

app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowEnrollmentSystem");

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Seed MAC sample data on startup (best-effort)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var adminSeeder = scope.ServiceProvider.GetRequiredService<AdminUserSeeder>();
        await adminSeeder.EnsureAdminUserAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Admin user seeding failed: {ex.Message}");
    }
}

app.Run();
