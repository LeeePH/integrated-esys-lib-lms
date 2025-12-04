using StudentPortal.Services;
using BCrypt.Net;
using System.Net; // ✅ Needed for TLS configuration
 System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

var builder = WebApplication.CreateBuilder(args);



// ✅ Force TLS 1.2 (important for MongoDB Atlas connections)
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

// ✅ Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<EmailService>();

// ✅ Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// ✅ Add MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ✅ Seed default admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedAdminAsync(services);
}

// ✅ Middleware configuration
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // session middleware

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

app.Run();


// ✅ Local function to seed admin
async Task SeedAdminAsync(IServiceProvider services)
{
    try
    {
        var mongo = services.GetRequiredService<MongoDbService>();
        var existing = await mongo.GetUserByEmailAsync("admin@mysuqc.local");

        if (existing == null)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Admin@1234");
            await mongo.CreateUserAsync(
                email: "admin@mysuqc.local",
                hashedPassword: hash,
                otp: "",
                role: "Admin",
                markVerified: true
            );

            Console.WriteLine("✅ Default Admin created: admin@mysuqc.local / Admin@1234");
        }
        else
        {
            Console.WriteLine("ℹ️ Admin account already exists.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error seeding admin: {ex.Message}");
    }
}
