using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public class CustomDataProtectionOptions
{
    public const string SectionName = "DataProtectionOptions";

    [Required]
    public string ApplicationName { get; set; } = "SoccerManager";

    public string StorageFlag { get; set; } = "EphemeralKeySet";

    [Required(ErrorMessage = "At least one certificate is required.")]
    [MinLength(1, ErrorMessage = "The Certificates list cannot be empty.")]
    public List<CertOptions> Certificates { get; set; } = [];
}

