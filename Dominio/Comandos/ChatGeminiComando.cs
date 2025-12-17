using Bitbucket_PR_Sentinel.Contratos;
using MediatR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Text.RegularExpressions;

namespace Bitbucket_PR_Sentinel.Dominio.Comandos;

public class ChatGeminiComando : IRequest<string>
{
    public string Prompt { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public double? Temperature { get; set; } = 0;
}

public class ChatGeminiHandler : IRequestHandler<ChatGeminiComando , string>
{
    private readonly Kernel _kernel;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ChatGeminiHandler> _logger;

    public ChatGeminiHandler(Kernel kernel , ICacheService cacheService , ILogger<ChatGeminiHandler> logger)
    {
        _kernel = kernel;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<string> Handle(ChatGeminiComando request , CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("O prompt não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(request.ConversationId))
            request.ConversationId = Guid.NewGuid().ToString();

        try
        {
            const string systemPrompt = """
                    Você é um assistente de engenharia de software especializado, chamado "Sentinel".
                    Sempre que possível, utilize as funções disponíveis (plugins) para executar tarefas solicitadas.
                    Priorize funções sobre respostas genéricas.
                    Ao listar PRs, inclua informações como ID, título e autor.
                    Você também pode analisar código e dar sugestões de melhorias e boas práticas.
                """;

            var executionSettings = new GeminiPromptExecutionSettings
            {
                Temperature = request.Temperature ?? 0.2 ,
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
            };

            var conversationHistory = await _cacheService.GetHistoryAsync(request.ConversationId);

            ChatHistory history = new();
            history.AddSystemMessage(systemPrompt);

            foreach (var message in conversationHistory)
            {
                if (message.Role.Equals("system" , StringComparison.OrdinalIgnoreCase))
                    continue;

                AuthorRole role = message.Role.ToLower() switch
                {
                    "user" => AuthorRole.User,
                    "assistant" => AuthorRole.Assistant,
                    "tool" => AuthorRole.Tool,
                    _ => AuthorRole.User
                };

                history.AddMessage(role, message.Content);
            }

            var sanitizedPrompt = Regex.Replace(request.Prompt , @"[^\u0000-\uFFFF]" , "");
            history.AddUserMessage(sanitizedPrompt);

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            var result = await chatCompletionService.GetChatMessageContentsAsync(
                history ,
                executionSettings: executionSettings ,
                kernel: _kernel ,
                cancellationToken: cancellationToken
            );

            var reply = result?.FirstOrDefault()?.Content ?? "Não obtive resposta.";

            // Cache
            await _cacheService.SaveMessageAsync(request.ConversationId , new Dominio.Modelos.Message { Role = "user" , Content = sanitizedPrompt });
            await _cacheService.SaveMessageAsync(request.ConversationId , new Dominio.Modelos.Message { Role = "assistant" , Content = reply });

            return reply;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao processar prompt no Gemini (ConversationId: {ConversationId})" , request.ConversationId);
            throw;
        }
    }
}
