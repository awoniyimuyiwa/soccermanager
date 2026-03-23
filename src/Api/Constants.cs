namespace Api;

public class Constants
{
    public const string AlreadyExistsErrorMessage = "Already exists";

    public const string AntiforgeryCookieName = "__Host-Antiforgery";

    public const string AntiforgeryJSReadableCookieName = "XSRF-TOKEN";

    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    public const string AntiforgeryValidationErrorMesage =  "Antiforgery token validation failed";

    public const string ConcurrentLoginErrorMessage = "Concurrent logins are prohibited. Please log out from other devices before logging in again.";

    public const string CountryCodeErrorMessage = "Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)";

    public const string DistributedLockResiliencePolicyName = "distributed_lock_resilience_policy";

    public const string GlobalRateLimitPolicyName = "global_rate_limit_policy";

    public const string InvalidErrorMessage = "Invalid";

    public const string LlmHttpClientName = "LlmHttpClient";

    public const int MaxLengthOfList = 100;

    public const int MaxLengthOfPlayers = 11;

    public const string SecretProtectorPurpose = "VaultedSecrets";

    public const string UserRateLimitPolicyName = "user_rate_limit_policy";
  
}
