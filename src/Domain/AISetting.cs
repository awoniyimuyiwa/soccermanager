namespace Domain;

public class AISetting : AuditedEntity
{
    public string? CustomEndpoint { get; set; }

    /// <summary>
    /// Encrypted Base64Url string in C#, varbinary(max) in SQL
    /// Nullable to support local LLMs
    /// </summary>
    [NotAudited]
    public string? Key { get; set; }

    public string Model { get; set; } = "";

    public AIProvider Provider { get; set; } = AIProvider.OpenAI;
}


public enum AIProvider
{
    OpenAI,

    Anthropic,

    Gemini,

    Groq,

    Ollama
}