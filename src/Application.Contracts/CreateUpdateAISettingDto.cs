using Domain;

namespace Application.Contracts;

public record CreateUpdateAISettingDto
{
    public virtual string? CustomEndpoint { get; init; }

    /// <summary>
    /// Encrypted Base64Url string in C#, varbinary(max) in SQL.
    /// Optional to support local LLMs like Ollama.
    /// </summary>
    [NotAudited]
    public virtual string? Key { get; init; }

    public virtual string Model { get; init; } = "";

    public virtual int Provider { get; init; } = (int)AIProvider.OpenAI;
}

