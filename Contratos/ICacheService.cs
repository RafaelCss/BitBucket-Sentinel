namespace Bitbucket_PR_Sentinel.Contratos;

public interface ICacheService
{
    Task<List<Dominio.Modelos.Message>> GetHistoryAsync(string conversationId);
    Task SaveMessageAsync(string conversationId , Dominio.Modelos.Message message);
    Task SaveHistoryAsync(string conversationId , List<Dominio.Modelos.Message> history);
}
