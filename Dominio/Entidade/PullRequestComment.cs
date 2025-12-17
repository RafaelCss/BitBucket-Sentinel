namespace Bitbucket_PR_Sentinel.Dominio.Entidade
{
    public class PullRequestComment
    {
        public int Id { get; set; }
        public string Author { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedOn { get; set; }
        public bool IsInline { get; set; }
    }
}
