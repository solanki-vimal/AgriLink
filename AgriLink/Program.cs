    using AgriLink.Data;
using AgriLink.Helpers;
using AgriLink.Models;
using AgriLink.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient<IMarketPriceProvider, AgmarknetPriceProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
}).RemoveAllLoggers(); // the request URL contains the API key, so don't log it

var app = builder.Build();

// Initialize Cloudinary if credentials are provided in configuration (otherwise falls back to local storage)
ImageUploadHelper.Initialize(app.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Intercept 404, 403, 500 status codes and re-execute through our friendly status handler
app.UseStatusCodePagesWithReExecute("/Home/StatusCodeHandler/{0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// Seed Farmer / Buyer / Admin roles, and an Admin user (credentials from environment), on startup
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var logger      = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                          .CreateLogger("AdminSeed");

    // 1. Ensure all roles exist
    string[] roleNames = { "Farmer", "Buyer", "Admin" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }

    // 2. Seed Admin user only if credentials are supplied via environment / user secrets
    //    Never hardcoded — set AdminSeed:Email and AdminSeed:Password in:
    //      Local  → dotnet user-secrets
    //      Render → Environment Variables dashboard (use __ double-underscore for nested keys)
    //               Key: AdminSeed__Email   Value: admin@agrilink.com
    //               Key: AdminSeed__Password  Value: <your password>
    var adminEmail    = app.Configuration["AdminSeed:Email"];
    var adminPassword = app.Configuration["AdminSeed:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            logger.LogInformation("Seeding admin user: {Email}", adminEmail);
            var adminUser = new ApplicationUser
            {
                UserName  = adminEmail,
                Email     = adminEmail,
                FullName  = "AgriLink Admin",
                Address   = "Platform HQ",
                CreatedAt = DateTime.Now
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Admin user seeded successfully.");
            }
            else
            {
                foreach (var error in result.Errors)
                    logger.LogError("Admin seed error: {Code} - {Description}", error.Code, error.Description);
            }
        }
        else
        {
            logger.LogInformation("Admin user already exists, skipping seed.");
        }
    }
    else
    {
        logger.LogWarning("AdminSeed:Email or AdminSeed:Password not set — admin user not seeded.");
    }
}

app.Run();
