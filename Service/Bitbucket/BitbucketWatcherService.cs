using Bitbucket_PR_Sentinel.Contratos;
using Bitbucket_PR_Sentinel.Dominio.Entidade;
using Microsoft.AspNetCore.SignalR;
using Bitbucket_PR_Sentinel.Service.Notificacao;
using MediatR;
using Bitbucket_PR_Sentinel.Dominio.Comandos;

public class BitbucketWatcherService : BackgroundService
{
    private readonly ILogger<BitbucketWatcherService> _logger;
    private readonly IBitbucketService _client;
    private readonly IConfiguration _config;
    private readonly Dictionary<int , PullRequestInfo> _knownPRs = new();
    private readonly IHubContext<NotificacaoHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    public BitbucketWatcherService(
            ILogger<BitbucketWatcherService> logger,
            IBitbucketService client,
            IConfiguration config,
            IHubContext<NotificacaoHub> hubContext,  
            IServiceProvider serviceProvider)
    {
        _logger = logger;
        _client = client;
        _config = config;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workspace = _config["Bitbucket:Workspace"];
        var repo = _config["Bitbucket:Repo"];

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var prs = await _client.GetOpenPRsAsync(workspace , repo);
                    foreach (var pr in prs)
                    {
                        if (!_knownPRs.ContainsKey(pr.Id))
                        {
                            _knownPRs.TryAdd(pr.Id , pr);
                            _logger.LogInformation("Nova PR detectada: {Title}" , pr.Title);

                            var prompt = $"""  
                               Você é um assistente de engenharia de software especializado, chamado "Sentinel".
                               Sempre que possível, utilize as funções disponíveis (plugins) para executar tarefas solicitadas.
                               Priorize funções sobre respostas genéricas.
                               Ao listar PRs, inclua informações como ID, título e autor.
                               Você também pode analisar código e dar sugestões de melhorias e boas práticas.

                               use as funções de obter_diff_arquivo , obter_diff_pr, obter_comentarios_pr ou analisar_codigo_pr
                           Analise o seguinte PR {pr.Id}:  

                           Gere um resumo claro do que foi alterado, incluindo:  
                           - Codigo alterado  
                           - Objetivo da mudança  
                           - Principais arquivos afetados  
                           - Possíveis riscos  
                           - Recomendação (aprovar, revisar, recusar)  
                           """;

                        var command = new ChatGeminiComando
                        {
                            Prompt = prompt ,
                            ConversationId = Guid.NewGuid().ToString()
                        };
                        using var scope = _serviceProvider.CreateScope();

                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        var result = await mediator.Send(command);
                            await _hubContext.Clients.All.SendAsync("receiveAnalysis" , new
                            {
                                prId = pr.Id ,
                                title = pr.Title ,
                                author = pr.Author ,
                                link = pr.Link ,
                                analysis = result ,
                                conversationId = command.ConversationId ,
                            });

                            await _hubContext.Clients.All.SendAsync("prOpen" , new
                            {
                                prId = pr.Id ,
                                title = pr.Title ,
                                author = pr.Author ,
                                link = pr.Link ,
                            });
                        }
                    }
              
            }
            catch (Exception ex)
            {
                _logger.LogError(ex , "Erro ao monitorar PRs");
            }

            await Task.Delay(TimeSpan.FromMinutes(2) , stoppingToken);
        }
    }
}