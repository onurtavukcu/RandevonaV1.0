using Domain.Entities.Appointment;
using Domain.Entities.Identity.UserEntity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Data.MongoDbContext
{
    public class MongoIndexInitializerHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MongoIndexInitializerHostedService> _logger;

        public MongoIndexInitializerHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<MongoIndexInitializerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<MongoSettings>();
            settings.Validate();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.StartupTimeoutSeconds));

            try
            {
                var database = scope.ServiceProvider.GetRequiredService<IControlMongoDbContext>().Database;
                await database.RunCommandAsync<BsonDocument>(
                    new BsonDocument("ping", 1), cancellationToken: timeout.Token);
                await MongoIndexes.EnsureControlAsync(database, timeout.Token);
                _logger.LogInformation("MongoDB connection and indexes are ready.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("MongoDB startup failed ({ErrorType}); application startup is stopped.",
                    ex.GetType().Name);
                throw new InvalidOperationException(
                    "MongoDB startup failed. Check connectivity, credentials, encryption configuration and index compatibility.",
                    ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

