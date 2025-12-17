using System.Text.Json.Serialization;

namespace Bitbucket_PR_Sentinel.Dominio.Modelos;

public record CommentContent(
        [property: JsonPropertyName("raw")] string Raw
    );
