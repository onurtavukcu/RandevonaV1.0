using Data.MongoDbContext;
using Domain.Models.Identity.User.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityService.Seeders;

// AddIdentityServices is registered after AddMongoPersistence, so central indexes are ready first.
public sealed class SystemAdminInitializerHostedService(IServiceScopeFactory scopeFactory,
    ILogger<SystemAdminInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetService<SystemAdminSettings>();
        if (settings is null || settings.Emails is { Length: 0 })
        {
            logger.LogInformation("System administrator setup skipped: no accounts configured.");
            return;
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(scope.ServiceProvider.GetRequiredService<MongoSettings>().StartupTimeoutSeconds));
        try
        {
            await scope.ServiceProvider.GetRequiredService<SystemAdminSeeder>().SeedAsync(settings, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError("System administrator setup failed ({ErrorType}); startup is stopped.", ex.GetType().Name);
            throw new InvalidOperationException("System administrator setup failed. Check SystemAdminSettings and database availability.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
