using Bitbucket_PR_Sentinel.Contratos;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Bitbucket_PR_Sentinel.Infra.Redis;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly DistributedCacheEntryOptions _cacheOptions;

    public RedisCacheService(IDistributedCache cache , ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
        // Configuração de expiração (Ex: Conversas expiram após 24 horas de inatividade)
        _cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };
    }

    private string GetKey(string conversationId) => $"chat:{conversationId}";

    public async Task<List<Dominio.Modelos.Message>> GetHistoryAsync(string conversationId)
    {
        var key = GetKey(conversationId);
        var cachedHistory = await _cache.GetStringAsync(key);

        if (string.IsNullOrEmpty(cachedHistory))
        {
            _logger.LogInformation("Nenhum histórico encontrado para {ConversationId}." , conversationId);
            return new List<Dominio.Modelos.Message>();
        }

        try
        {
            var history = JsonSerializer.Deserialize<List<Dominio.Modelos.Message>>(cachedHistory);
            return history ?? new List<Dominio.Modelos.Message>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex , "Erro ao desserializar histórico do Redis para {ConversationId}." , conversationId);
            // Retorna lista vazia para não quebrar
            return new List<Dominio.Modelos.Message>();
        }
    }

    public async Task SaveMessageAsync(string conversationId , Dominio.Modelos.Message message)
    {
        var currentHistory = await GetHistoryAsync(conversationId);
        currentHistory.Add(message);

        await SaveHistoryAsync(conversationId , currentHistory);
    }

    public async Task SaveHistoryAsync(string conversationId , List<Dominio.Modelos.Message> history)
    {
        var key = GetKey(conversationId);
        var serializedHistory = JsonSerializer.Serialize(history);

        await _cache.SetStringAsync(key , serializedHistory , _cacheOptions);
    }
}