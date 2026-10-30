using BaseMVC.Infrastructure.Identity;
using BaseMVC.Application;
using BaseMVC.Application.Auditing;
using BaseMVC.Application.Users;
using BaseMVC.Application.Roles;
using BaseMVC.Application.Permissions;
using BaseMVC.Application.Accounts;
using BaseMVC.Application.Faculties;
using BaseMVC.Application.Students;
using BaseMVC.Infrastructure.Faculties;
using BaseMVC.Infrastructure.Students;
using BaseMVC.Infrastructure.Persistence;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaseMVC.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplication();
        services.AddScoped<IAuditLogReadStore, AuditLogReadStore>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IRoleManagementService, RoleManagementService>();
        services.AddScoped<IPermissionManagementService, PermissionManagementService>();
        services.AddScoped<IFacultyManagementService, FacultyManagementService>();
        services.AddScoped<IStudentManagementService, StudentManagementService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAccountPasswordService, AccountPasswordService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required for Identity persistence.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.EventsType = typeof(ApplicationCookieEvents);
        });

        services.AddHttpContextAccessor();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<ApplicationCookieEvents>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();

        return services;
    }
}
