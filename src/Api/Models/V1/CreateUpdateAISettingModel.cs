using Domain;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

/// <summary>
/// Model for creating or updating AI settings.
/// </summary>
/// <param name="CustomEndpoint">Optional custom URL (e.g., http://localhost:11434).</param>
/// <param name="Key">The API key; encrypted at rest. Optional for local LLMs.</param>
/// <param name="Model">The specific model name (e.g., 'gpt-4o' or 'llama3.1').</param>
/// <param name="Provider">
/// The AI provider identifier:
/// 0 = <see cref="AIProvider.OpenAI"/>, 
/// 1 = <see cref="AIProvider.Anthropic"/>, 
/// 2 = <see cref="AIProvider.Gemini"/>, 
/// 3 = <see cref="AIProvider.Groq"/>, 
/// 4 = <see cref="AIProvider.Ollama"/>.
/// </param>
public record CreateUpdateAISettingModel(
    [Url(ErrorMessage = "Please provide a valid absolute URL (e.g., http://localhost:11434).")]
    [MaxLength(Domain.Constants.StringMaxLength)]
    string? CustomEndpoint,

    [property: NotAudited]
    [DataType(DataType.Password)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    string? Key,

    [Required(ErrorMessage = "A Model name is required.")]
    [MaxLength(Domain.Constants.StringMaxLength)]
    string Model = "",

    [Required]
    [EnumDataType(typeof(AIProvider))]
    int Provider = (int)AIProvider.OpenAI);
