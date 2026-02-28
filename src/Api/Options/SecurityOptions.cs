using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public class SecurityOptions 
{
    public const string SectionName = "SecurityOptions";

    /// <summary>
    /// Specifies the maximum lifespan of a cookie-based authentication session. 
    /// Controls the 'Expires' attribute in the browser and the TTL of the session in Redis.
    /// Does not affect Bearer token expiration.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "14.00:00:00", ErrorMessage = "CookieTimeout must be between 1 minute and 14 days.")]
    [Required]
    public TimeSpan CookieTimeout { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Specifies how the system handles multiple concurrent login attempts for the same user.
    /// Defaults to <see cref="LoginConcurrencyMode.KickOut"/>.
    /// </summary>
    [Required]
    [EnumDataType(typeof(LoginConcurrencyMode))]
    public LoginConcurrencyMode LoginConcurrencyMode { get; set; }

    /// <summary>
    /// When true, the system will issue a new refresh token upon each use of an existing refresh token, and invalidate the old one.
    /// </summary>
    public bool ShouldRotateRefreshTokens { get; set; } = true;
}

public enum LoginConcurrencyMode
{
    /// <summary>
    /// Standard behavior: Users can log in from as many devices as they want.
    /// No sessions are ever terminated by new logins.
    /// </summary>
    AllowMultiple,

    /// <summary>
    /// SaaS behavior: New logins "kick out" any existing session for that user.
    /// Prioritizes the most recent user activity.
    /// </summary>
    KickOut,

    /// <summary>
    /// Banking behavior: New logins are strictly rejected if an active session exists.
    /// Ensures only one traceable actor is present at a time.
    /// </summary>
    Block
}


