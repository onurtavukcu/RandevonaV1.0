using IdentityService.LoginService;
using IdentityService.PasswordService;
using IdentityService.RegisterService;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Extensions;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddScoped<IPasswordService, PasswordService.PasswordService>();
        services.AddScoped<IRegisterService, RegisterService.RegisterService>();
        services.AddScoped<ILoginService, LoginService.LoginService>();
        return services;
    }
}
