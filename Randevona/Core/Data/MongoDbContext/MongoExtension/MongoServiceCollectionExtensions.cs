using CommonServices.WorkContext.ContextAccessor;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Data.MongoDataEncryption;
using Data.Repositories.BaseRepositories;
using Data.Repositories.Identity.Login;
using Data.Repositories.Identity.Management;
using Data.Repositories.Identity.Registration;
using Data.Repositories.MailTemplates;
using Domain.Models.MongoEncriyption;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Data.MongoDbContext.MongoExtension
{
    public static class MongoServiceCollectionExtensions
    {
        public static IServiceCollection AddMongoPersistence(this IServiceCollection services)
        {
            services.AddSingleton<IMongoClient>(sp =>
            {
                var settings = sp.GetRequiredService<MongoSettings>();
                settings.Validate();
                MongoEncryptionMapping.Register(sp.GetRequiredService<EncryptionSettings>());
                var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
                clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(settings.StartupTimeoutSeconds);
                clientSettings.ConnectTimeout = TimeSpan.FromSeconds(Math.Min(10, settings.StartupTimeoutSeconds));
                return new MongoClient(clientSettings);
            });
            services.AddSingleton<IControlMongoDbContext, ControlMongoDbContext>();
            services.AddScoped<TenantWorkContextHolder>();
            services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
            services.AddScoped<ITenantWorkContextResolver, TenantWorkContextResolver>();
            services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
            services.AddScoped<TenantProvisioningService>();
            services.AddScoped<IRegistrationRepository, RegistrationRepository>();
            services.AddScoped<ILoginRepository, LoginRepository>();
            services.AddScoped<IManagementRepository, ManagementRepository>();
            services.AddScoped<IMongoDbContext, MongoDbContext>();
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped(typeof(IScopedRepository<>), typeof(ScopedRepository<>));
            services.AddScoped<IMailTemplateRepository, MailTemplateRepository>();
            services.AddHostedService<MongoIndexInitializerHostedService>();
            return services;
        }
    }
}

