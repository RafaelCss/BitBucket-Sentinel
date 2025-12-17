using Bitbucket_PR_Sentinel.Service.Bitbucket;
using Microsoft.SemanticKernel;
using Bitbucket_PR_Sentinel.Contratos;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Bitbucket_PR_Sentinel.Service.Bitbucket.Plugins;


namespace Bitbucket_PR_Sentinel.IoC.Olhama;

public static class OllamaServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguraOllama(this IServiceCollection services , IConfiguration configuration)
    {
        // 1️⃣ Lê as configurações
        var ollamaApiKey = configuration["Ollama:ApiKey"]
            ?? throw new InvalidOperationException("A chave 'Ollama:ApiKey' não foi encontrada na configuração.");

        var ollamaEndpoint = new Uri(configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434");
        var ollamaModelId = configuration.GetValue<string>("Ollama:ModelId") ?? "llama3.1:8b";


        services.AddSingleton<Kernel>(sp =>
        {

            var bitbucketService = sp.GetRequiredService<IBitbucketService>();
            var logger = sp.GetRequiredService<ILogger<BitbucketPlugin>>();
            var plugin = new BitbucketPlugin(bitbucketService , configuration , logger);

            var kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.AddOllamaChatCompletion(
                modelId: "llama3.1:8b" ,
                endpoint: new Uri("http://localhost:11434")
            );

            kernelBuilder.Plugins.AddFromObject(plugin , "BitbucketPlugin");
            // Reutiliza o mesmo builder acima
            kernelBuilder.Services.AddLogging();

            return kernelBuilder.Build();
        });


        return services;
    }
}