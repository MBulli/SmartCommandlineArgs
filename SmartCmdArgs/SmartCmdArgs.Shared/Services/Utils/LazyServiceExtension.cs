using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartCmdArgs.Services.Utils
{
    public static class LazyServiceExtension
    {
        public static void AddLazySingleton<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            services.AddSingleton<TService, TImplementation>();
            services.AddSingleton(x => new Lazy<TService>(() => x.GetRequiredService<TService>()));
        }

        public static void AddLazySingleton<TService>(this IServiceCollection services)
            where TService : class
        {
            services.AddSingleton<TService>();
            services.AddSingleton(x => new Lazy<TService>(() => x.GetRequiredService<TService>()));
        }

        public static void AddLazySingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            services.AddSingleton(implementationFactory);
            services.AddSingleton(x => new Lazy<TService>(() => x.GetRequiredService<TService>()));
        }
    }
}
