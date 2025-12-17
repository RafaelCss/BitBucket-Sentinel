using Bitbucket_PR_Sentinel.Contratos;
using Bitbucket_PR_Sentinel.Dominio.Comandos;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp.Models.Chat;

namespace Bitbucket_PR_Sentinel.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GeminiController : ControllerBase
    {
        private readonly Kernel _kernel;
        private readonly ILogger<GeminiController> _logger;
        private readonly IMediator _mediator;

        public GeminiController(ILogger<GeminiController> logger , IConfiguration configuration , Kernel kernel , ICacheService cacheService, IMediator mediator)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mediator = mediator;
        }

        [HttpPost("chat/ollama")]
        public async Task<IActionResult> ChatOllamaAsync([FromBody] Dominio.Modelos.ChatRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                _logger.LogWarning("Prompt inválido recebido no endpoint /chat");
                return BadRequest("O prompt não pode ser vazio.");
            }

            try
            {
                // Prompt do sistema definindo regras para uso de plugins
                var systemPrompt = """
                  Você é um assistente de engenharia de software especializado em Bitbucket, chamado "PR Sentinel".
                  Sempre que possível, utilize as funções disponíveis (plugins) para executar tarefas solicitadas.
                  Priorize funções sobre respostas genéricas.
                  Ao listar PRs, inclua informações como ID, título e autor.
                  """;

                var executionSettings = new OllamaPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() ,
                };

                _logger.LogInformation("Invocando Kernel para prompt do usuário: {prompt}" , request.Prompt);

                ChatHistory history = new();
                history.AddSystemMessage(systemPrompt);
                history.AddUserMessage(request.Prompt);

                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

                var resultContents = await chatCompletionService.GetChatMessageContentsAsync(
                    history ,
                    executionSettings: executionSettings ,
                    kernel: _kernel ,
                    cancellationToken: CancellationToken.None
                );

                var reply = resultContents?.FirstOrDefault()?.Content ?? "Não obtive resposta.";

                return Ok(new { reply });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex , "Erro ao processar o prompt via Kernel");
                return StatusCode(500 , $"Erro ao processar o prompt: {ex.Message}");
            }
        }


        [HttpPost("chat/gemini")]
        public async Task<IActionResult> ChatGeminiAsync([FromBody] ChatGeminiComando command)
        {
            try
            {
                var reply = await _mediator.Send(command);
                return Ok(new { reply , conversationId = command.ConversationId });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex , "Erro de validação no prompt");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex , "Erro inesperado no chat Gemini");
                return StatusCode(500 , $"Erro interno: {ex.Message}");
            }
        }
    }
}