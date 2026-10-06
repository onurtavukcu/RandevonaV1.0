using IntegrationServices.Whatsapp;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationServices.Extensions;
public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrationServices(this IServiceCollection services)
    {
        services.AddHttpClient<IMetaPhoneClient, MetaPhoneClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 262144;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
          .RemoveAllLoggers();
        return services;
    }
}

