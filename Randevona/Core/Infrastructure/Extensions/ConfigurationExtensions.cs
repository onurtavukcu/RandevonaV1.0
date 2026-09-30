using Domain.Models.Shared.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Infrastructure.Extensions
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection AddAppSettings(this IServiceCollection services, IConfiguration configuration)
        {
            var settingTypes = GetApplicationAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t =>
                    typeof(ISettings).IsAssignableFrom(t) &&
                    t.IsClass &&
                    !t.IsAbstract &&
                    !t.ContainsGenericParameters)
                .Distinct();

            var configureMethod = typeof(OptionsConfigurationServiceCollectionExtensions)
                .GetMethods()
                .Single(method =>
                    method.Name == "Configure" &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == 2 &&
                    method.GetParameters()[0].ParameterType == typeof(IServiceCollection) &&
                    method.GetParameters()[1].ParameterType == typeof(IConfiguration));

            foreach (var type in settingTypes)
            {
                var section = configuration.GetSection(type.Name);

                if (!section.Exists())
                {
                    continue;
                }

                configureMethod.MakeGenericMethod(type)
                    .Invoke(null, new object[] { services, section });

                var optionsType = typeof(IOptions<>).MakeGenericType(type);
                services.AddSingleton(type, sp =>
                {
                    var options = sp.GetRequiredService(optionsType);
                    var valueProperty = optionsType.GetProperty(nameof(IOptions<object>.Value));
                    return valueProperty!.GetValue(options)!;
                });
            }

            return services;
        }

        private static IEnumerable<Assembly> GetApplicationAssemblies()
        {
            var assemblies = new HashSet<Assembly>
            {
                typeof(ISettings).Assembly,
                typeof(ConfigurationExtensions).Assembly
            };

            if (Assembly.GetEntryAssembly() is { } entryAssembly)
            {
                assemblies.Add(entryAssembly);
            }

            // Referenced project DLLs can be present in the output before .NET loads them.
            foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            {
                AssemblyName assemblyName;
                try
                {
                    assemblyName = AssemblyName.GetAssemblyName(path);
                }
                catch (BadImageFormatException)
                {
                    // Native DLLs do not contain .NET settings classes.
                    continue;
                }

                assemblies.Add(Assembly.Load(assemblyName));
            }

            return assemblies;
        }
    }
}
