using Bitbucket_PR_Sentinel.Contratos;
using Bitbucket_PR_Sentinel.Infra.Redis;
using System.Reflection;

namespace Bitbucket_PR_Sentinel.IoC.Infra
{
    public static class ConfigInfra
    {
        public static IServiceCollection AddConfiguracaoInfra(this IServiceCollection services , IConfiguration configuration)
        {

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            });

            services.AddScoped<ICacheService , RedisCacheService>();

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "BitbucketPRSentinel:";
            });

            return services;
        }
    }
}
