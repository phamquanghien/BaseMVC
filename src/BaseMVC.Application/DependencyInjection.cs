using BaseMVC.Application.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace BaseMVC.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(SearchAuditLogsQuery).Assembly));
        return services;
    }
}
