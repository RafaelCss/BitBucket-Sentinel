using Bitbucket_PR_Sentinel.Contratos;
using Bitbucket_PR_Sentinel.Dominio.Entidade;
using Bitbucket_PR_Sentinel.Dominio.Modelos;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Bitbucket_PR_Sentinel.Service.Bitbucket;

public class BitbucketService : IBitbucketService
{
    private readonly HttpClient _http;
    private readonly ILogger<BitbucketService> _logger;
    private readonly IConfiguration _configuration;


    public BitbucketService(HttpClient http , ILogger<BitbucketService> logger, IConfiguration configuration)
    {
        _http = http;
        _logger = logger;
        _configuration = configuration;
    }

    #region ======= PRs abertas =======
    public async Task<List<PullRequestInfo>> GetOpenPRsAsync(string workspace , string repoSlug)
    {

        var url = $"repositories/{workspace}/{repoSlug}/pullrequests?state=OPEN";
        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Erro ao consultar PRs do Bitbucket: {Status}" , response.StatusCode);
            return new();
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var result = new List<PullRequestInfo>();

        foreach (var pr in doc.RootElement.GetProperty("values").EnumerateArray())
        {
            var reviewers = new List<string>();
            if (pr.TryGetProperty("reviewers" , out var revArray))
            {
                reviewers.AddRange(
                    revArray.EnumerateArray()
                        .Select(r => r.GetProperty("display_name").GetString() ?? "")
                );
            }

            result.Add(new PullRequestInfo
            {
                Id = pr.GetProperty("id").GetInt32() ,
                Title = pr.GetProperty("title").GetString() ?? "" ,
                Author = pr.GetProperty("author").GetProperty("display_name").GetString() ?? "" ,
                Link = pr.GetProperty("links").GetProperty("html").GetProperty("href").GetString() ?? "" ,
                CreatedOn = pr.GetProperty("created_on").GetDateTime() ,
                UpdatedOn = pr.GetProperty("updated_on").GetDateTime() ,
                Reviewers = reviewers
            });
        }

        return result;
    }
    #endregion

    #region ======= Declinar PR =======
    public async Task<bool> DeclinePullRequestAsync(
        string workspace ,
        string repoSlug ,
        int prId ,
        string username)
    {

        try
        {
            // Monta o endpoint
            var url = $"https://api.bitbucket.org/2.0/repositories/{workspace}/{repoSlug}/pullrequests/{prId}/decline";

            // Pega as credenciais de configuração (email + token)
            var email = _configuration["Bitbucket:Email"]
                        ?? throw new InvalidOperationException("Variável BITBUCKET_EMAIL não configurada.");
            var token = _configuration["Bitbucket:Token"]      ?? throw new InvalidOperationException("Variável BITBUCKET_TOKEN não configurada.");

            // Cria um request com autenticação Basic
            var request = new HttpRequestMessage(HttpMethod.Post , url);
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{token}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic" , authValue);

            // Envia o request
            var resp = await _http.SendAsync(request);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Falha ao declinar PR {prId}: {status} - {content}" , prId , resp.StatusCode , content);
                return false;
            }

            _logger.LogInformation("PR {prId} foi declinada com sucesso." , prId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao declinar PR {prId}" , prId);
            return false;
        }
    }
    #endregion

    #region ======= Adicionar comentário =======
    public async Task<bool> AddCommentAsync(string workspace , string repoSlug , int prId , string comment , string? filePath = null , int? lineFrom = null , int? lineTo = null)
    {
        var url = $"repositories/{workspace}/{repoSlug}/pullrequests/{prId}/comments";

        var payload = new Dictionary<string , object>
        {
            ["content"] = new { raw = comment }
        };

        // Inline comment (ligado a um arquivo/linha específica)
        if (!string.IsNullOrEmpty(filePath))
        {
            payload["inline"] = new
            {
                path = filePath ,
                from = lineFrom ,
                to = lineTo
            };
        }

        var json = JsonSerializer.Serialize(payload , new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var resp = await _http.PostAsync(url , new StringContent(json , Encoding.UTF8 , "application/json"));
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Erro ao adicionar comentário ao PR {prId}: {status}" , prId , resp.StatusCode);
            return false;
        }

        _logger.LogInformation("Comentário adicionado ao PR {prId}" , prId);
        return true;
    }
    #endregion

    #region ======= Buscar diff do PR =======
    public async Task<string?> GetPullRequestDiffAsync(string workspace , string repoSlug , int prId)
    {
        var url = $"repositories/{workspace}/{repoSlug}/pullrequests/{prId}/diff";

        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Erro ao obter diff do PR {prId}: {status}" , prId , resp.StatusCode);
            return null;
        }

        return await resp.Content.ReadAsStringAsync();
    }
    #endregion

    #region ======= Buscar comentários =======
    public async Task<List<PullRequestComment>> GetPullRequestCommentsAsync(string workspace , string repoSlug , int prId)
    {
        var results = new List<PullRequestComment>();
        var url = $"repositories/{workspace}/{repoSlug}/pullrequests/{prId}/comments";

        while (!string.IsNullOrEmpty(url))
        {
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Erro ao buscar comentários do PR {prId}: {status}" , prId , resp.StatusCode);
                break;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            foreach (var item in doc.RootElement.GetProperty("values").EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var raw = item.GetProperty("content").GetProperty("raw").GetString() ?? "";
                var author = item.GetProperty("user").GetProperty("display_name").GetString() ?? "";
                var created = item.GetProperty("created_on").GetDateTime();
                var isInline = item.TryGetProperty("inline" , out var inlineProp) && inlineProp.ValueKind != JsonValueKind.Null;

                results.Add(new PullRequestComment
                {
                    Id = id ,
                    Author = author ,
                    Content = raw ,
                    CreatedOn = created ,
                    IsInline = isInline
                });
            }

            url = doc.RootElement.TryGetProperty("next" , out var next) ? next.GetString() ?? "" : "";
        }

        return results;
    }
    #endregion

    #region Detalhes das Alterações do PR (Diffstat)

    /// <summary>
    /// Obtém um resumo (diffstat) dos arquivos alterados em um Pull Request.
    /// Útil para saber quais arquivos buscar o diff detalhado (se necessário).
    /// </summary>
    /// <returns>Uma lista de entradas Diffstat.</returns>
    public async Task<List<DiffstatEntry>> GetPRDiffstatAsync(string workspace , string repoSlug , int prId)
    {
        // Monta o endpoint
        var url = $"https://api.bitbucket.org/2.0/repositories/{workspace}/{repoSlug}/pullrequests/{prId}/diffstat";
        /// Pega as credenciais de configuração (email + token)
        var email = _configuration["Bitbucket:Email"] ?? "";
        var token = _configuration["Bitbucket:Token"] ?? "";

        // Cria um request com autenticação Basic
        var request = new HttpRequestMessage(HttpMethod.Get , url);
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{token}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic" , authValue);

        // Se você estiver configurando o HttpClientHandler:
        var handler = new HttpClientHandler()
        {
            // CRÍTICO: Certifique-se de que esta propriedade é TRUE
            AllowAutoRedirect = true
        };

        var _http = new HttpClient(handler);
        // Envia o request
        var resp = await _http.SendAsync(request);
        var content = await resp.Content.ReadAsStringAsync();

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound || resp.StatusCode == System.Net.HttpStatusCode.Redirect)
        {
            // 1. Obtém a nova URL do cabeçalho Location
            var redirectUrl = resp?.RequestMessage?.RequestUri?.AbsoluteUri;

            if (string.IsNullOrEmpty(redirectUrl))
            {
                // Trate erro: Redirecionamento sem URL de destino
                return new List<DiffstatEntry>();
            }
            _http = new HttpClient(handler);
            // 2. Cria e envia uma nova requisição para a URL redirecionada
            var redirectRequest = new HttpRequestMessage(HttpMethod.Get , redirectUrl);

            // 3. Reaplica a autenticação Basic para a nova requisição
            redirectRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic" , authValue);

            resp = await _http.SendAsync(redirectRequest);
            // O restante do fluxo continuará a partir daqui com a nova resposta
             content = await resp.Content.ReadAsStringAsync();

        }

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Erro ao consultar diffstat do PR #{PrId} no Bitbucket: {Status}" , prId , resp.StatusCode);
            return new List<DiffstatEntry>();
        }

        try
        {
            var json = content;

            // Usando System.Text.Json Source Generator (ou JsonSerializer) para deserialização moderna
            // Nota: Você precisará ajustar sua configuração de serialização, mas vou usar o JsonSerializer padrão.
            var doc = JsonDocument.Parse(json);
            var values = doc.RootElement.GetProperty("values").ToString();

            // Deserializa diretamente para a lista de DTOs
            var result = JsonSerializer.Deserialize<List<DiffstatEntry>>(values);

            return result ?? new List<DiffstatEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex , "Erro ao processar a resposta do diffstat do PR #{PrId}." , prId);
            return new List<DiffstatEntry>();
        }
    }

    #endregion

    #region Comentar Inline em um Arquivo

    /// <summary>
    /// Publica um comentário em uma linha específica dentro de um Pull Request.
    /// É crucial que 'lineNumber' corresponda à linha no arquivo de DESTINO (TO).
    /// </summary>
    /// <param name="path">O caminho do arquivo alterado (ex: 'src/File.cs').</param>
    /// <param name="lineNumber">O número da linha no arquivo final (depois do merge).</param>
    /// <returns>True se o comentário foi postado com sucesso.</returns>
    public async Task<bool> PostInlineCommentAsync(string workspace , string repoSlug , int prId , string commentText , string path , int lineNumber)
    {
        // 1. Criar o DTO da requisição (Payload)
        var requestBody = new InlineCommentRequest(
            new CommentContent(commentText) ,
            new InlineAnchor(path , lineNumber)
        );

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonPayload , Encoding.UTF8 , "application/json");

        // 2. Montar e Enviar a Requisição POST
        var url = $"repositories/{workspace}/{repoSlug}/pullrequests/{prId}/comments";
        var response = await _http.PostAsync(url , content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Erro ao postar comentário inline no PR #{PrId}. Status: {Status}. Detalhes: {Error}" ,
                             prId , response.StatusCode , errorContent);

            // Crítico: O Bitbucket retorna 400 Bad Request se a linha não for válida no diff
            return false;
        }

        _logger.LogInformation("Comentário inline postado com sucesso no arquivo {Path} na linha {LineNumber} do PR #{PrId}." ,
                               path , lineNumber , prId);
        return true;
    }

    #endregion


    /// <summary>
    /// Obtém o diff detalhado (conteúdo linha por linha) de um arquivo alterado em um PR.
    /// </summary>
    /// <param name="entry">O objeto DiffstatEntry para o arquivo a ser analisado.</param>
    /// <returns>A string do diff no formato unified.</returns>
    public async Task<string> GetFileDiffAsync(string workspace , string repoSlug , DiffstatEntry entry)
    {
        string targetHash = ExtractHashFromSrcUrl(entry?.OldFile?.Links?.Self?.Href)?? "";
        string sourceHash = ExtractHashFromSrcUrl(entry?.NewFile?.Links?.Self?.Href) ?? "";

        if (string.IsNullOrEmpty(sourceHash))
        {
            return string.Empty;
        }

        string encodedFilePath = WebUtility.UrlEncode(entry?.NewFile?.Path) ?? "";
        var spec = $"{sourceHash}..{targetHash}";
  
        var diffUrl = $"https://api.bitbucket.org/2.0/repositories/{workspace}/{repoSlug}/diff/{spec}?path={encodedFilePath}&topic=true";


        var email = _configuration["Bitbucket:Email"] ?? "";
        var token = _configuration["Bitbucket:Token"] ?? "";
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{token}"));
        var authHeader = new AuthenticationHeaderValue("Basic" , authValue);

        var request = new HttpRequestMessage(HttpMethod.Get , diffUrl); 

        var resp = await _http.SendAsync(request);
        var content = await resp.Content.ReadAsStringAsync();

        if (resp.IsSuccessStatusCode)
        {
            return content;
        }
        else
        {
            return string.Empty;
        }
    }

    private string ExtractHashFromSrcUrl(string? srcUrl)
    {
        if (string.IsNullOrEmpty(srcUrl))
            return null;
        var parts = srcUrl.Split('/');

        var srcIndex = Array.IndexOf(parts , "src");

        if (srcIndex > 0 && parts.Length > srcIndex + 1)
        {
            return parts[srcIndex + 1];
        }
        return null;
    }
}
