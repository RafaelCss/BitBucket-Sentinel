

using Bitbucket_PR_Sentinel.Contratos;
using Bitbucket_PR_Sentinel.Service.Bitbucket;
using System.Net.Http.Headers;

namespace Bitbucket_PR_Sentinel.IoC.Dominio;

public static class ConfigDominio
{
    public static IServiceCollection AddConfiguracaoDominio(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IBitbucketService , BitbucketService>((sp , client) =>
        {
            var token = configuration["Bitbucket:TokenConsulta"]
                ?? throw new InvalidOperationException("Bitbucket:Token não encontrado na configuração.");

            client.BaseAddress = new Uri("https://api.bitbucket.org/2.0/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer" , token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
