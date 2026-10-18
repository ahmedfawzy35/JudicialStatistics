using JudicialStatistics.Infrastructure;
using JudicialStatistics.Web.Components;
using JudicialStatistics.Web.Components.Account;
using JudicialStatistics.Web.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==================== Razor / Blazor ====================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<JudicialStatistics.Web.Services.CurrentCourtState>();
builder.Services.AddScoped<JudicialStatistics.Web.Services.UserManagementService>();

// ==================== Authentication ====================
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// ==================== Database ====================
// ApplicationDbContext يرث من JudicialDbContextBase{ApplicationUser}
// يحتوي جميع جداول التطبيق + Identity مع ApplicationUser
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// خدمات Infrastructure (JudicialDbContext مع IdentityUser للـ Seed)
builder.Services.AddInfrastructureServices(builder.Configuration);

// ==================== Identity ====================
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // في بيئة التطوير: لا يشترط تأكيد البريد
        options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment();
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        // إعدادات الحماية وقفل الحساب عند المحاولات الخاطئة (Brute Force Protection)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // إعدادات كلمات المرور
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// ==================== Seed ====================
// تنفيذ الـ Seed عند البدء (في أي بيئة)
await DatabaseInitializer.InitializeAsync(app.Services);
await AdminUserSeeder.SeedAdminUserAsync(app.Services);

// ==================== Middleware ====================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.Run();
