using Bitbucket_PR_Sentinel.Contratos;
using Microsoft.AspNetCore.SignalR;

namespace Bitbucket_PR_Sentinel.Service.Notificacao;

public class NotificacaoHub : Hub
{
    private readonly IBitbucketService _bitbucketService;
    private readonly IConfiguration _config;

    public NotificacaoHub(IBitbucketService bitbucketService , IConfiguration config)
    {
        _bitbucketService = bitbucketService;
        _config = config;
    }

    public async Task HandleUserResponse(string action , int prId , string? comment = null)
    {
        var workspace = _config["Bitbucket:Workspace"];
        var repo = _config["Bitbucket:Repo"];

        switch (action.ToLowerInvariant())
        {
            case "aceitar":
                await Clients.All.SendAsync("receiveMessage" , $"✅ PR #{prId} marcada como aceita.");
                break;
            case "recusar":
                await _bitbucketService.DeclinePullRequestAsync(workspace , repo , prId  , comment ?? "Recusada via chat");
                await Clients.All.SendAsync("receiveMessage" , $"❌ PR #{prId} recusada. Motivo: {comment}");
                break;
            case "comentar":
                await _bitbucketService.AddCommentAsync(workspace , repo , prId , comment ?? "Comentário via chat");
                await Clients.All.SendAsync("receiveMessage" , $"💬 Comentário adicionado à PR #{prId}: {comment}");
                break;
        }
    }
}