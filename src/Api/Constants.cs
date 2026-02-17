namespace Api;

public class Constants
{
    public const string AlreadyExistsErrorMessage = "Already exists";

    public const string CountryCodeErrorMessage = "Must be a valid ISO 3166-1 alpha-2 country code (e.g., US, GB)";

    public const string DistributedLockResiliencePolicyName = "distributed_lock_resilience_policy";

    public const string GlobalRateLimitPolicyName = "global_rate_limit_policy";

    public const int MaxLengthOfPlayers = 11;

    public const string UserRateLimitPolicyName = "user_rate_limit_policy";
}
