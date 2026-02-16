using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public class RateLimitOptions
{
    public const string SectionName = "RateLimitOptions";

    /// <summary>
    /// Global limit (e.g., 5000 requests per minute total for the whole API)
    /// This limit is applied when the total traffic on the instance is too high, saving Redis CPU cycles and preventing DDOS
    /// </summary>
    [Range(1, 10000)]
    public int GlobalLimit { get; set; } = 5000;

    /// <summary>
    /// Unauthenticated user limit based on IP (e.g., 20 requests per minute per IP)
    /// </summary>
    [Range(1, 10000)]
    public int GuestLimit { get; set; } = 20;

    /// <summary>
    /// Max 24 hours
    /// </summary>
    [Range(1, 1440)] 
    public int Minutes { get; set; } = 1;

    /// <summary>
    /// Authenticated user limit (e.g., 100 requests per minute per user)
    /// </summary>
    [Range(1, 10000)]
    public int UserLimit { get; set; } = 100;

    /// <summary>
    /// List of IPs or UserNames that bypass the limit
    /// </summary>
    public List<string> WhiteList { get; set; } = [];

}