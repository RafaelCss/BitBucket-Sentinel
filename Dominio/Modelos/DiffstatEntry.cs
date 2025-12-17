using System.Text.Json.Serialization;

namespace Bitbucket_PR_Sentinel.Dominio.Modelos;

public record DiffstatEntry(
        [property: JsonPropertyName("status")] string Status ,
        [property: JsonPropertyName("lines_added")] int LinesAdded ,
        [property: JsonPropertyName("lines_removed")] int LinesRemoved ,
        [property: JsonPropertyName("old")] DiffFile OldFile ,
        [property: JsonPropertyName("new")] DiffFile NewFile
    );
