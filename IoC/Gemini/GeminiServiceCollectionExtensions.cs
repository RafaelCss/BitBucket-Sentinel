using Bitbucket_PR_Sentinel.Contratos;
using Bitbucket_PR_Sentinel.Service.Bitbucket.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;



namespace Bitbucket_PR_Sentinel.IoC.Gemini;

public static class GeminiServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguraGemini(this IServiceCollection services , IConfiguration configuration)
    {
        var geminiApiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("A chave 'Gemini:ApiKey' não foi encontrada na configuração.");

        var geminiModelId = configuration.GetValue<string>("Gemini:GeminiModelId") ?? "gemma-3-4b";

        services
             .AddGoogleAIGeminiChatCompletion(
                 modelId: "gemini-2.5-flash" ,
                 apiKey: geminiApiKey);


        services.AddSingleton<Kernel>(sp =>
        {

            var bitbucketService = sp.GetRequiredService<IBitbucketService>();
            var logger = sp.GetRequiredService<ILogger<BitbucketPlugin>>();
            var loggerReview = sp.GetRequiredService<ILogger<BitbucketCodeReviewPlugin>>();
            var plugin = new BitbucketPlugin(bitbucketService , configuration , logger);

            var kernelBuilder = Kernel.CreateBuilder();

            var chatService = sp.GetRequiredService<IChatCompletionService>();
            kernelBuilder.Services.AddSingleton(chatService);


            kernelBuilder.Plugins.AddFromObject(plugin , "BitbucketPlugin");
            // Reutiliza o mesmo builder acima
            kernelBuilder.Services.AddLogging();

            return kernelBuilder.Build();
        });

        return services;
    }
}
