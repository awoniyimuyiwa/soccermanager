using Application.Contracts;
using Domain;
using System.ComponentModel.DataAnnotations;

namespace Api.Models.V1;

public record CreateUpdateAISettingModel : CreateUpdateAISettingDto
{
    [MaxLength(Domain.Constants.StringMaxLength)]
    [Url(ErrorMessage = "Please provide a valid absolute URL (e.g., http://localhost:11434).")]
    public override string? CustomEndpoint { get; init; }

    /// <summary>
    /// Optional to support local LLMs like Ollama.
    /// Encrypted at rest
    /// </summary>
    [NotAudited]
    [DataType(DataType.Password)]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string? Key { get; init; }

    [Required(ErrorMessage = "A Model name (e.g., 'gpt-4o' or 'llama3.1') is required.")]
    [MaxLength(Domain.Constants.StringMaxLength)]
    public override string Model { get; init; } = "";

    [Required]
    [EnumDataType(typeof(AIProvider))]
    public override int Provider { get; init; }
}


