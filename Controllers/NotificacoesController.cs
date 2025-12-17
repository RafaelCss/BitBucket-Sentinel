using Microsoft.AspNetCore.Mvc;

namespace Bitbucket_PR_Sentinel.Controllers;

public class NotificacoesController : ControllerBase
{
    [HttpGet]
    [Route("api/[controller]")]
    public IActionResult GetNotificacoes()
    {
        var notificacoes = new[]
        {
            new { titulo = "Nova Pull Request", descricao = "Você foi adicionado como revisor na PR #23", tempo = "há 2 horas" },
            new { titulo = "PR aprovada", descricao = "Sua PR #45 foi aprovada", tempo = "há 10 minutos" }
        };

        return Ok(notificacoes);
    }
}
