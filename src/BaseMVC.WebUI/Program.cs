using BaseMVC.Infrastructure;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.WebUI.SqliteBackups;
using BaseMVC.WebUI.Auditing;
using BaseMVC.WebUI.Email;
using BaseMVC.WebUI.HealthChecks;
using BaseMVC.WebUI.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Globalization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<AuditLogRetentionService>();
builder.Services
    .AddOptions<SqliteBackupOptions>()
    .Bind(builder.Configuration.GetSection("SqliteBackup"))
    .Validate(options => IsValidBackupOptions(options), "SQLite backup configuration is invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton<SqliteBackupStatus>();
builder.Services.AddSingleton<IBackupArchiveStore, FileSystemBackupArchiveStore>();
builder.Services.AddHostedService<SqliteDatabaseBackupService>();
builder.Services.AddRateLimiter(options =>
{
    var accountRateLimits = builder.Configuration
        .GetSection("RateLimits:Account")
        .Get<AccountRateLimitOptions>() ?? new AccountRateLimitOptions();
    accountRateLimits.Validate();

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Bạn thao tác quá nhanh. Vui lòng thử lại sau.",
            cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value;
        var (policyName, permitLimit, windowSeconds) =
            context.Request.Method == HttpMethods.Post && string.Equals(
                path,
                AccountRateLimitPolicies.LoginPath,
                StringComparison.OrdinalIgnoreCase)
                ? (
                    AccountRateLimitPolicies.Login,
                    accountRateLimits.LoginPermitLimit,
                    accountRateLimits.LoginWindowSeconds)
                : context.Request.Method == HttpMethods.Post && string.Equals(
                    path,
                    AccountRateLimitPolicies.PasswordResetPath,
                    StringComparison.OrdinalIgnoreCase)
                    ? (
                        AccountRateLimitPolicies.PasswordReset,
                        accountRateLimits.PasswordResetPermitLimit,
                        accountRateLimits.PasswordResetWindowSeconds)
                    : (null, 0, 0);

        if (policyName is null)
        {
            return RateLimitPartition.GetNoLimiter("unlimited");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{policyName}:{GetRateLimitPartitionKey(context)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddCheck<SqliteReadinessHealthCheck>("sqlite", tags: ["ready"]);
if (builder.Configuration.GetSection("SqliteBackup").Get<SqliteBackupOptions>()?.Enabled ?? true)
{
    builder.Services
        .AddHealthChecks()
        .AddCheck<SqliteBackupHealthCheck>("sqlite-backup", tags: ["ready"]);
}

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await identitySeeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    await next();

    var statusCode = context.Response.StatusCode;
    if (statusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        || context.Response.HasStarted
        || context.Response.ContentLength is not null)
    {
        return;
    }

    var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
    context.Request.Path = $"/Account/StatusCodePage/{statusCode}";
    context.Request.QueryString = QueryString.Create("returnUrl", returnUrl);
    context.SetEndpoint(null);
    context.Response.ContentType = null;
    context.Response.ContentLength = null;
    await next();
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthStatusAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthStatusAsync
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

static string GetRateLimitPartitionKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static bool IsValidBackupOptions(SqliteBackupOptions options)
{
    try
    {
        options.Validate();
        return true;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
}

static Task WriteHealthStatusAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "text/plain";
    return context.Response.WriteAsync(report.Status.ToString());
}

public partial class Program
{
}
