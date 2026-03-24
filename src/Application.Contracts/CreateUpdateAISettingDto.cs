using Domain;

namespace Application.Contracts;

/// <summary>
/// DTO for creating or updating AI settings.
/// </summary>
/// <param name="CustomEndpoint">Optional custom URL for the AI service.</param>
/// <param name="Key">Encrypted Base64Url key; optional for local LLMs like Ollama.</param>
/// <param name="Model">The specific model name (e.g., gpt-4).</param>
/// <param name="Provider">The AI provider identifier (Default: OpenAI).</param>
public record CreateUpdateAISettingDto(
    string? CustomEndpoint,
    [property: NotAudited] string? Key,
    string Model = "",
    int Provider = (int)AIProvider.OpenAI);