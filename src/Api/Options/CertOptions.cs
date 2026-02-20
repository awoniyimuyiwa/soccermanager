using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public class CertOptions    
{
    /// <summary>
    /// The cert should be stored as base64 in config to avoid file management issues across different environments (local dev, docker, cloud).
    /// </summary>
    [Required]
    public string Base64 { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

