using Microsoft.Extensions.DependencyInjection;
using System;

namespace SmartCmdArgs.Services
{
    public static class FactoryServiceExtension
    {
        public static void AddFactory<T>(this IServiceCollection services)
            where T : class
        {
            services.AddTransient<T>();
            services.AddSingleton<IFactory<T>, Factory<T>>();
        }
    }

    public interface IFactory<T>
    {
        T Create();
    }

    internal class Factory<T> : IFactory<T>
    {
        private readonly IServiceProvider serviceProvider;

        public Factory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public T Create()
        {
            return serviceProvider.GetRequiredService<T>();
        }
    }
}
