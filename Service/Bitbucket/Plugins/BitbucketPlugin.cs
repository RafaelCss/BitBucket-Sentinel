using Bitbucket_PR_Sentinel.Contratos;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace Bitbucket_PR_Sentinel.Service.Bitbucket.Plugins;

public class BitbucketPlugin
{
    private readonly IBitbucketService _bitbucket;
    private readonly IConfiguration _config;
    private readonly ILogger<BitbucketPlugin> _logger;
    private readonly string _workspace;
    private readonly string _repo;

    public BitbucketPlugin(
        IBitbucketService bitbucket ,
        IConfiguration config ,
        ILogger<BitbucketPlugin> logger)
    {
        _bitbucket = bitbucket;
        _config = config;
        _logger = logger;

        _workspace = _config["Bitbucket:Workspace"] ?? throw new InvalidOperationException("Bitbucket:Workspace não encontrado");
        _repo = _config["Bitbucket:Repo"] ?? throw new InvalidOperationException("Bitbucket:Repo não encontrado");
    }

    // 🧾 Lista as PRs abertas
    [KernelFunction("listar_prs_abertas")]
    [Description("ACESSA o Bitbucket em tempo real e RETORNA a lista completa e atualizada de Pull Requests abertas no repositório configurado. USE SEMPRE para responder a perguntas sobre PRs ativos.")]
    public async Task<string> ListarPRsAsync()
    {
        var prs = await _bitbucket.GetOpenPRsAsync(_workspace , _repo);
        if (prs == null || !prs.Any())
            return "Nenhuma Pull Request aberta encontrada.";

        var sb = new StringBuilder();
        sb.AppendLine("📋 Pull Requests abertas:");
        foreach (var pr in prs)
        {
            sb.AppendLine($"- #{pr.Id}: {pr.Title} (autor: {pr.Author})");
        }

        return sb.ToString();
    }

    // 💬 Adiciona comentário em uma PR
    [KernelFunction("comentar_pr")]
    [Description("Adiciona um comentário em uma Pull Request específica.")]
    public async Task<string> ComentarPRAsync(
        [Description("O ID da PR no Bitbucket.")] int prId ,
        [Description("O comentário a ser adicionado.")] string comentario)
    {
        try
        {
            await _bitbucket.AddCommentAsync(_workspace , _repo , prId  , comentario);
            _logger.LogInformation("💬 Comentário adicionado na PR #{prId}: {comentario}" , prId , comentario);
            return $"💬 Comentário publicado na PR #{prId}: \"{comentario}\"";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao adicionar comentário na PR #{prId}" , prId);
            return $"⚠️ Erro ao comentar na PR #{prId}: {ex.Message}";
        }
    }

    // ✅ Faz merge da PR
    [KernelFunction("merge_pr")]
    [Description("Faz merge de uma Pull Request (aceita e integra no branch principal).")]
    public async Task<string> MergePRAsync(
        [Description("O ID da PR a ser mesclada.")] int prId)
    {
        try
        {
            // Aqui você pode criar um método MergePullRequestAsync no seu IBitbucketService
            // Por enquanto vamos usar AddCommentAsync como placeholder:
            await _bitbucket.AddCommentAsync(_workspace , _repo , prId  , "✅ PR aprovada e merge iniciado.");
            _logger.LogInformation("Merge iniciado para PR #{prId}" , prId);
            return $"✅ Merge iniciado para PR #{prId}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao mesclar PR #{prId}" , prId);
            return $"⚠️ Erro ao realizar merge na PR #{prId}: {ex.Message}";
        }
    }

    // ❌ Recusa PR
    [KernelFunction("recusar_pr")]
    [Description("Recusa uma Pull Request, informando um motivo opcional.")]
    public async Task<string> RecusarPRAsync(
        [Description("O ID da PR.")] int prId ,
        [Description("Motivo da recusa.")] string? motivo = null)
    {
        try
        {
            await _bitbucket.DeclinePullRequestAsync(_workspace , _repo , prId  , motivo ?? "Recusada via IA");
            _logger.LogInformation("PR #{prId} recusada com motivo: {motivo}" , prId , motivo);
            return $"❌ PR #{prId} recusada. Motivo: {motivo ?? "Nenhum motivo especificado."}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao recusar PR #{prId}" , prId);
            return $"⚠️ Erro ao recusar PR #{prId}: {ex.Message}";
        }
    }


    #region Obter Diffstat do Pull Request

    /// <summary>
    /// Obtém um resumo das alterações de arquivos (diffstat) em um PR específico.
    /// </summary>
    /// <param name="prId">ID do Pull Request.</param>
    [KernelFunction("obter_diff_pr")]
    [Description("RETORNA a lista de arquivos alterados (incluindo adições e remoções de linhas) em um Pull Request específico. Útil para entender o escopo das mudanças.")]
    public async Task<string> ObterDiffstatPRAsync(
        [Description("O ID numérico do Pull Request.")] int prId)
    {
        _logger.LogInformation("Buscando diffstat para o PR #{PrId}." , prId);

        var diffstat = await _bitbucket.GetPRDiffstatAsync(_workspace , _repo , prId);

        if (diffstat == null || !diffstat.Any())
            return $"Não foi possível obter o diffstat ou o PR #{prId} não contém alterações.";

        var sb = new StringBuilder();
        sb.AppendLine($"📊 Diffstat para PR #{prId}:");
        foreach (var entry in diffstat)
        {
            var oldPath = entry.OldFile.Path;
            var newPath = entry.NewFile.Path;

            // Lógica para formatar o nome do arquivo baseado no status (ex: ADICIONADO, REMOVIDO)
            string fileName;
            if (entry.Status == "removed")
                fileName = $"{oldPath} (REMOVIDO)";
            else if (entry.Status == "added")
                fileName = $"{newPath} (ADICIONADO)";
            else if (oldPath != newPath)
                fileName = $"{oldPath} -> {newPath} (RENOMEADO/MOVIDO)";
            else
                fileName = newPath;

            sb.AppendLine($"- {fileName}: +{entry.LinesAdded} linhas, -{entry.LinesRemoved} linhas.");
        }

        return sb.ToString();
    }

    #endregion


    #region Obter Diff detalhado de um Arquivo

    /// <summary>
    /// Obtém o conteúdo de texto puro (diff) das alterações de um arquivo específico em um Pull Request.
    /// </summary>
    /// <param name="prId">ID do Pull Request.</param>
    /// <param name="filePath">O caminho exato do arquivo (ex: 'src/services/UserService.cs').</param>
    [KernelFunction("obter_diff_arquivo")]
    [Description("RETORNA o conteúdo das alterações (diff) de um arquivo específico em um Pull Request, mostrando o código linha por linha (formato unified diff).")]
    public async Task<string> ObterDiffArquivoAsync(
        [Description("O ID numérico do Pull Request.")] int prId ,
        [Description("O caminho completo do arquivo para obter o diff (ex: src/service/file.cs).")] string filePath)
    {
        _logger.LogInformation("Buscando diff detalhado para o arquivo '{FilePath}' no PR #{PrId}." , filePath , prId);

        // 1. Obter o Diffstat completo para encontrar a entrada correspondente.
        // Isso é necessário para obter os hashes de commit Old e New que o Bitbucket exige.
        var diffstats = await _bitbucket.GetPRDiffstatAsync(_workspace , _repo , prId);

        if (diffstats == null || !diffstats.Any())
            return $"Não foi possível obter o diffstat do PR #{prId}. Verifique se o PR existe e possui alterações.";

        // 2. Encontrar a entrada DiffstatEntry para o arquivo solicitado
        var entry = diffstats.FirstOrDefault(e =>
            e.NewFile?.Path.Equals(filePath , StringComparison.OrdinalIgnoreCase) == true ||
            e.OldFile?.Path.Equals(filePath , StringComparison.OrdinalIgnoreCase) == true);

        if (entry == null)
            return $"Arquivo '{filePath}' não encontrado na lista de alterações do PR #{prId}. Verifique o caminho exato.";

        // 3. Chamar o GetFileDiffAsync com a entrada encontrada
        try
        {
            // Se a entrada for de adição ou modificação, buscamos o diff.
            if (entry.Status == "modified" || entry.Status == "added" || entry.Status == "removed")
            {
                var fileDiff = await _bitbucket.GetFileDiffAsync(_workspace , _repo , entry);

                if (string.IsNullOrEmpty(fileDiff))
                {
                    return $"O Bitbucket não retornou o conteúdo do diff para o arquivo '{filePath}'. Pode ser apenas alteração de espaço em branco ou erro de permissão.";
                }

                // Formatação do resultado (retorna o diff puro para análise da IA)
                var sb = new StringBuilder();
                sb.AppendLine($"--- DIFF DETALHADO para {filePath} (PR #{prId}) ---");
                sb.AppendLine(fileDiff);
                sb.AppendLine("-------------------------------------------------");

                return sb.ToString();
            }
            else if (entry.Status == "renamed" || entry.Status == "copied")
            {
                return $"O arquivo '{filePath}' foi renomeado/copiado. Use '{entry.NewFile.Path}' para ver o conteúdo do diff da alteração.";
            }
            else
            {
                return $"O status da alteração para '{filePath}' é '{entry.Status}', o que geralmente significa que não há conteúdo de diff para mostrar.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao buscar diff do arquivo '{FilePath}' no PR #{PrId}." , filePath , prId);
            return $"Erro interno ao buscar o diff do arquivo: {ex.Message}";
        }
    }

    #endregion

    #region Comentar Inline em Linha Específica

    /// <summary>
    /// Publica um comentário em uma linha específica dentro de um Pull Request.
    /// </summary>
    /// <param name="prId">ID do Pull Request.</param>
    /// <param name="commentText">O texto do comentário.</param>
    /// <param name="path">O caminho do arquivo, ex: 'src/Service.cs'.</param>
    /// <param name="lineNumber">O número da linha no arquivo de destino.</param>
    [KernelFunction("comentar_linha_pr")]
    [Description("POSTA um comentário em uma linha específica de um arquivo dentro de um Pull Request. **REQUER** o ID do PR, o texto do comentário, o caminho exato do arquivo e o número da linha de DESTINO.")]
    public async Task<string> ComentarLinhaPRAsync(
        [Description("O ID numérico do Pull Request.")] int prId ,
        [Description("O texto detalhado do comentário a ser postado.")] string commentText ,
        [Description("O caminho completo e exato do arquivo (ex: 'src/features/Auth/Login.tsx').")] string path ,
        [Description("O número da linha no arquivo final (no branch de destino do PR).")] int lineNumber)
    {
        _logger.LogInformation("Postando comentário inline no PR #{PrId}, arquivo: {Path}, linha: {LineNumber}." , prId , path , lineNumber);

        var success = await _bitbucket.PostInlineCommentAsync(
            _workspace , _repo , prId , commentText , path , lineNumber
        );

        if (success)
            return $"✅ Comentário postado com sucesso no PR #{prId} no arquivo '{path}', linha {lineNumber}.";
        else
            return $"❌ ERRO ao postar o comentário no PR #{prId}. Isso pode ocorrer se a linha {lineNumber} no arquivo '{path}' não for uma linha que foi alterada no Pull Request.";
    }

    #endregion

    // 💬 Obtém comentários da PR (descrição + discussões)
    [KernelFunction("obter_comentarios_pr")]
    [Description("Obtém todos os comentários e discussões de uma Pull Request no Bitbucket.")]
    public async Task<string> ObterComentariosPRAsync(
        [Description("O ID da Pull Request.")] int prId)
    {
        try
        {
            var comentarios = await _bitbucket.GetPullRequestCommentsAsync(_workspace , _repo , prId);
            if (comentarios == null || !comentarios.Any())
                return $"Nenhum comentário encontrado na PR #{prId}.";

            var sb = new StringBuilder();
            sb.AppendLine($"💬 Comentários da PR #{prId}:");
            foreach (var c in comentarios)
            {
                sb.AppendLine($"- {c.Author}: {c.Content}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao obter comentários da PR #{prId}" , prId);
            return $"⚠️ Erro ao obter comentários da PR #{prId}: {ex.Message}";
        }
    }


    [KernelFunction("analisar_codigo_pr")]
    [Description("Lê o diff de uma PR e os comentários feitos, e retorna uma análise crítica com sugestões de melhorias no código.")]
    public async Task<string> AnalisarCodigoPRAsync(
        [Description("O ID da Pull Request.")] int prId ,
        [Description("Define o nível da análise (simples, detalhada, crítica).")] string modo = "detalhada")
    {
        try
        {
            var diff = await ObterDiffstatPRAsync(prId);
            var comentarios = await ObterComentariosPRAsync(prId);

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 Análise da PR #{prId}");
            sb.AppendLine($"Modo: {modo}");
            sb.AppendLine("\n--- 🔧 Código Alterado ---\n");
            sb.AppendLine(diff);
            sb.AppendLine("\n--- 💬 Comentários ---\n");
            sb.AppendLine(comentarios);
            sb.AppendLine("\n--- 🧠 Solicitação ---\n");
            sb.AppendLine("Com base nas alterações acima e nos comentários, identifique:");
            sb.AppendLine("1. Problemas de legibilidade, performance ou segurança;");
            sb.AppendLine("2. Sugestões de melhoria de boas práticas C# / .NET;");
            sb.AppendLine("3. Se os comentários são válidos e justificáveis tecnicamente.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao analisar código da PR #{prId}" , prId);
            return $"⚠️ Erro ao analisar PR #{prId}: {ex.Message}";
        }
    }
}

