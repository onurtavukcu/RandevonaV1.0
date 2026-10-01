using IdentityService.LoginService;
using IdentityService.PasswordService;
using IdentityService.RegisterService;
using IdentityService.Seeders;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Extensions;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddScoped<IPasswordService, PasswordService.PasswordService>();
        services.AddScoped<IRegisterService, RegisterService.RegisterService>();
        services.AddScoped<ILoginService, LoginService.LoginService>();
        services.AddScoped<SystemAdminSeeder>();
        services.AddHostedService<SystemAdminInitializerHostedService>();
        return services;
    }
}
