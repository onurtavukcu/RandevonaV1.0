using BussinessServices.ProviderService;
using Microsoft.Extensions.DependencyInjection;

namespace BussinessServices.Extensions;

public static class BussinessServiceCollectionExtensions
{
    public static IServiceCollection AddBussinessServices(this IServiceCollection services)
    {
        services.AddScoped<IProviderService, ProviderService.ProviderService>();
        services.AddScoped<ProviderNumberService.IProviderNumberResolver, ProviderNumberService.ProviderNumberResolver>();
        return services;
    }
}
