using System.Text.Json.Serialization;

namespace Bitbucket_PR_Sentinel.Dominio.Modelos;

public class BitbucketDiffstatResponse
{
    [JsonPropertyName("values")]
    public List<DiffstatEntry> Values { get; set; } = new List<DiffstatEntry>();

    [JsonPropertyName("pagelen")]
    public int PageLen { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }
}
