using Bitbucket_PR_Sentinel.Dominio.Entidade;
using Bitbucket_PR_Sentinel.Dominio.Modelos;
using System.Threading.Tasks;

namespace Bitbucket_PR_Sentinel.Contratos
{
    public interface IBitbucketService
    {
        Task<List<PullRequestInfo>> GetOpenPRsAsync(string workspace , string repoSlug);
        Task<bool> DeclinePullRequestAsync(string workspace , string repoSlug , int prId ,string? message = null);
        Task<bool> AddCommentAsync(string workspace , string repoSlug , int prId , string comment , string? filePath = null , int? lineFrom = null , int? lineTo = null);
        Task<string?> GetPullRequestDiffAsync(string workspace , string repoSlug , int prId);
        Task<List<PullRequestComment>> GetPullRequestCommentsAsync(string workspace , string repoSlug , int prId );
        Task<List<DiffstatEntry>> GetPRDiffstatAsync(string workspace , string repoSlug , int prId);
        Task<bool> PostInlineCommentAsync(string workspace , string repoSlug , int prId , string commentText , string path , int lineNumber);
        Task<string> GetFileDiffAsync(string workspace , string repoSlug , DiffstatEntry entry);
    }
}
