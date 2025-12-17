using System.Text.Json.Serialization;

namespace Bitbucket_PR_Sentinel.Dominio.Modelos;

public record InlineAnchor(
        [property: JsonPropertyName("path")] string Path ,
        [property: JsonPropertyName("to")] int To
    );
