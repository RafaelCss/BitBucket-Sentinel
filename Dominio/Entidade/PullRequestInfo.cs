namespace Bitbucket_PR_Sentinel.Dominio.Entidade;

public class PullRequestInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
    public List<string> Reviewers { get; set; } = new();
}
