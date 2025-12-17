namespace Bitbucket_PR_Sentinel.Dominio.Modelos;

public class Message
{
    public string Role { get; set; } = string.Empty; 
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
