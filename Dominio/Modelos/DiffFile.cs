using System.Text.Json.Serialization;

namespace Bitbucket_PR_Sentinel.Dominio.Modelos;

public record DiffFile(
    [property: JsonPropertyName("path")] string Path ,
    // CRÍTICO: Links é necessário para extrair o hash do commit no Old/New
    [property: JsonPropertyName("links")] DiffLinks Links
);

public record DiffLinks(
    [property: JsonPropertyName("self")] Link Self
);

public record Link(
    [property: JsonPropertyName("href")] string Href
);

