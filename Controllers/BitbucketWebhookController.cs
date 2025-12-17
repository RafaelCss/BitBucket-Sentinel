using Bitbucket_PR_Sentinel.Service.Notificacao;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace Bitbucket_PR_Sentinel.Controllers;

[ApiController]
[Route("api/bitbucket-webhook")]
public class BitbucketWebhookController : ControllerBase
{
    private readonly IHubContext<NotificacaoHub> _hubContext;

    public BitbucketWebhookController(IHubContext<NotificacaoHub> hubContext)
    {
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] JsonElement payload)
    {
        // TODO: validar hash do webhook

        if (payload.TryGetProperty("eventKey" , out var eventKeyProp))
        {
            var eventKey = eventKeyProp.GetString();
            if (eventKey == "pr:reviewer:updated")
            {
                var addedReviewers = payload.GetProperty("addedReviewers");
                foreach (var reviewer in addedReviewers.EnumerateArray())
                {
                    var username = reviewer.GetProperty("user").GetProperty("name").GetString();
                    var prTitle = payload.GetProperty("pullRequest").GetProperty("title").GetString();

                    await _hubContext.Clients.User(username)
                        .SendAsync("ReceiveNotification" , $"Você foi adicionado como revisor no PR: '{prTitle}'");
                }
            }
        }
        return Ok();
    }
}
