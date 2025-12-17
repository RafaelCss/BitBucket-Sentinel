using System.Text.Json.Serialization;

namespace Bitbucket_PR_Sentinel.Dominio.Modelos;

public record InlineCommentRequest(
        [property: JsonPropertyName("content")] CommentContent Content ,
        [property: JsonPropertyName("inline")] InlineAnchor Inline
    );
