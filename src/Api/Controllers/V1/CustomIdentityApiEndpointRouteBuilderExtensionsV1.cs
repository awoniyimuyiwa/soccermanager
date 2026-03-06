using Api.Extensions;
using Api.Models.V1;
using Api.Options;
using Api.Services;
using Application.Contracts;
using Domain;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace Api.Controllers.V1;

/// <summary>
/// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add identity endpoints.
/// </summary>
public static class CustomIdentityApiEndpointRouteBuilderExtensionsV1
{
    // Validate the email address using DataAnnotations like the UserValidator does when RequireUniqueEmail = true.
    private static readonly EmailAddressAttribute _emailAddressAttribute = new();

    /// <summary>
    /// Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">The type describing the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
    /// <param name="endpoints">
    /// The <see cref="IEndpointRouteBuilder"/> to add the identity endpoints to.
    /// Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)"/> to add a prefix to all the endpoints.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> to further customize the added endpoints.</returns>
    public static IEndpointConventionBuilder MapCustomIdentityApiV1<TUser>(this IEndpointRouteBuilder endpoints)
        where TUser : ApplicationUser, new()
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var timeProvider = endpoints.ServiceProvider.GetRequiredService<TimeProvider>();
        var bearerTokenOptions = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        var emailSender = endpoints.ServiceProvider.GetRequiredService<IEmailSender<TUser>>();
        var linkGenerator = endpoints.ServiceProvider.GetRequiredService<LinkGenerator>();

        // We'll figure out a unique endpoint name based on the final route pattern during endpoint generation.
        string? confirmEmailEndpointName = null;

        #region CustomCode
        var authGroup = endpoints.MapGroup("auth")
            .WithTags("Auth");
        
        // Endpoint for SPAs to fetch the initial antiforgery token required for 
        // state-changing requests (POST, PUT, PATCH, DELETE) and login attempts 
        // when using Cookie Authentication.
        authGroup.MapGet("/antiforgery-token", (
            HttpContext httpContext,
            [FromServices] IServiceProvider sp) =>
        {
            // Generates a new token bound to the current session (Anonymous or Authenticated)
            // and appends the 'XSRF-TOKEN' cookie to the response.
            httpContext.GetAndStoreAntiforgeryToken();

            return TypedResults.NoContent();
        }).WithSummary("Initializes the XSRF token for the client.")
        .WithDescription($"Sets the {Constants.AntiforgeryCookieName} and {Constants.AntiforgeryJSReadableCookieName} cookies. Copy the value of {Constants.AntiforgeryJSReadableCookieName} and set the {Constants.AntiforgeryHeaderName} header when making state-changing, cookie-authenticated requests.");

        // Endpoint for SPAs and mobile apps to terminate the user session. 
        // This invalidates the session in the Redis store (revoking access/refresh tokens),
        // clears the Authentication Cookie, and triggers the 'OnSigningOut' event 
        // to remove the 'XSRF-TOKEN' cookie from the browser.
        authGroup.MapPost("/logout", async (
            HttpContext context,
            SignInManager<ApplicationUser> signInManager,
            IOptionsMonitor<BearerTokenOptions> bearerOptions,
            IUserSessionManager sessionManager) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var accessToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : null;

            if (!string.IsNullOrWhiteSpace(accessToken)) 
            {
                await sessionManager.RemoveProtected(
                    accessToken,
                    null!); // Framework uses null for purpose
            }

            // Clear the Identity Cookie (for browsers)
            await signInManager.SignOutAsync();

            return TypedResults.NoContent();
        }).RequireAuthorization()
        .WithSummary("Logs out the user and invalidates only the current session.")
        .WithDescription("Clears security cookies, revokes the access/refresh session in Redis, and removes the client-side antiforgery token.");
        #endregion

        // NOTE: We cannot inject UserManager<TUser> directly because the TUser generic parameter is currently unsupported by RDG.
        // https://github.com/dotnet/aspnetcore/issues/47338    
        authGroup.MapPost("/register", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] RegisterRequest registration, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();

            if (!userManager.SupportsUserEmail)
            {
                throw new NotSupportedException($"{nameof(MapCustomIdentityApiV1)} requires a user store with email support.");
            }

            var userStore = sp.GetRequiredService<IUserStore<TUser>>();
            var emailStore = (IUserEmailStore<TUser>)userStore;
            var email = registration.Email;

            if (string.IsNullOrEmpty(email) || !_emailAddressAttribute.IsValid(email))
            {
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email)));
            }

            #region CustomCode
            var user = new TUser
            {
                ExternalId = Guid.NewGuid()
            };
            #endregion

            await userStore.SetUserNameAsync(user, email, CancellationToken.None);
            await emailStore.SetEmailAsync(user, email, CancellationToken.None);
            var result = await userManager.CreateAsync(user, registration.Password);

            if (!result.Succeeded)
            {
                return CreateValidationProblem(result);
            }

            #region CustomCode
            await CreateDefaultTeam(user, sp);
            #endregion

            await SendConfirmationEmailAsync(user, userManager, context, email);
            return TypedResults.Ok();
        });

        #region CustomCode
        // Force persistent cookies by ignoring 'useSessionCookies'. 
        // Since sessions are stored in Redis, the browser must be prevented from 
        // 'silently' deleting the cookie upon closure without notifying the server.
        // This ensures the Browser and Redis TTL remain synchronized.
        authGroup.MapPost("/login", async Task<Results<Ok<AccessTokenResponse>, EmptyHttpResult, ProblemHttpResult>>(
            [FromBody] LoginRequest login, 
            [FromQuery] bool? useCookies,
            HttpContext httpContext,
            [FromServices] IServiceProvider sp,
            [FromServices] IAntiforgery antiforgery) =>
        {
            var useCookieScheme = useCookies == true;
            var isPersistent = true;
          
            if (useCookieScheme)
            {
                // Defends against "Login CSRF" attacks where an attacker tricks a victim 
                // into logging into the attacker's account to capture sensitive user data.
                try
                {
                    await antiforgery.ValidateRequestAsync(httpContext);
                }
                catch (AntiforgeryValidationException)
                {
                    return TypedResults.Problem(Constants.AntiforgeryValidationErrorMesage, statusCode: StatusCodes.Status400BadRequest);
                }
            }

            var signInManager = sp.GetRequiredService<SignInManager<ApplicationUser>>();
            signInManager.AuthenticationScheme = useCookieScheme ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

            var result = await signInManager.PasswordSignInAsync(login.Email, login.Password, isPersistent, lockoutOnFailure: true);

            if (result.RequiresTwoFactor)
            {
                if (!string.IsNullOrEmpty(login.TwoFactorCode))
                {
                    result = await signInManager.TwoFactorAuthenticatorSignInAsync(login.TwoFactorCode, isPersistent, rememberClient: isPersistent);
                }
                else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                {
                    result = await signInManager.TwoFactorRecoveryCodeSignInAsync(login.TwoFactorRecoveryCode);
                }
            }

            if (!result.Succeeded)
            {
                return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);
            }

            if (useCookieScheme)
            {
                httpContext.GetAndStoreAntiforgeryToken();
            }
            
            // The signInManager already produced the needed response in the form of a cookie or bearer token.
            return TypedResults.Empty;
        }).DisableAntiforgery(); // <--- This prevents the group filter from running;
        #endregion

        authGroup.MapPost("/refresh", async Task<Results<Ok<AccessTokenResponse>, UnauthorizedHttpResult, SignInHttpResult, ChallengeHttpResult>>
            ([FromBody] RefreshRequest refreshRequest,
            [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<ApplicationUser>>();
            var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
            var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

            // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
            if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
                timeProvider.GetUtcNow() >= expiresUtc ||
                await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not ApplicationUser user)

            {
                return TypedResults.Challenge();
            }

            #region CustomCode
            var securityOptions = sp.GetRequiredService<IOptions<SecurityOptions>>().Value;
            if (securityOptions.ShouldRotateRefreshTokens)
            {
                var sessionManager = sp.GetRequiredService<IUserSessionManager>();
                await sessionManager.RemoveProtected(
                    refreshRequest.RefreshToken,
                    null!); // Framework uses null for purpose
            }
            #endregion

            var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
            return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        }).DisableAntiforgery(); // <--- This prevents the group filter from running

        authGroup.MapGet("/confirm-email", async Task<Results<ContentHttpResult, UnauthorizedHttpResult>>
            ([FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByIdAsync(userId) is not { } user)
            {
                // We could respond with a 404 instead of a 401 like Identity UI, but that feels like unnecessary information.
                return TypedResults.Unauthorized();
            }

            try
            {
                code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return TypedResults.Unauthorized();
            }

            IdentityResult result;

            if (string.IsNullOrEmpty(changedEmail))
            {
                result = await userManager.ConfirmEmailAsync(user, code);
            }
            else
            {
                // As with Identity UI, email and user name are one and the same. So when we update the email,
                // we need to update the user name.
                result = await userManager.ChangeEmailAsync(user, changedEmail, code);

                if (result.Succeeded)
                {
                    result = await userManager.SetUserNameAsync(user, changedEmail);
                }
            }

            if (!result.Succeeded)
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.Text("Thank you for confirming your email.");
        })
        .Add(endpointBuilder =>
        {
            var finalPattern = ((RouteEndpointBuilder)endpointBuilder).RoutePattern.RawText;
            confirmEmailEndpointName = $"{nameof(MapCustomIdentityApiV1)}-{finalPattern}";
            endpointBuilder.Metadata.Add(new EndpointNameMetadata(confirmEmailEndpointName));
        });

        authGroup.MapPost("/resend-confirmation-email", async Task<Ok>
            ([FromBody] ResendConfirmationEmailRequest resendRequest, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.FindByEmailAsync(resendRequest.Email) is not { } user)
            {
                return TypedResults.Ok();
            }

            await SendConfirmationEmailAsync(user, userManager, context, resendRequest.Email);
            return TypedResults.Ok();
        });

        authGroup.MapPost("/forgot-password", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] ForgotPasswordRequest resetRequest, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            var user = await userManager.FindByEmailAsync(resetRequest.Email);

            if (user is not null && await userManager.IsEmailConfirmedAsync(user))
            {
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                await emailSender.SendPasswordResetCodeAsync(user, resetRequest.Email, HtmlEncoder.Default.Encode(code));
            }

            // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
            // returned a 400 for an invalid code given a valid user email.
            return TypedResults.Ok();
        });

        authGroup.MapPost("/reset-password", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] ResetPasswordRequest resetRequest, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync(resetRequest.Email);

            if (user is null || !await userManager.IsEmailConfirmedAsync(user))
            {
                // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
                // returned a 400 for an invalid code given a valid user email.
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken()));
            }

            IdentityResult result;
            try
            {
                var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetRequest.ResetCode));
                result = await userManager.ResetPasswordAsync(user, code, resetRequest.NewPassword);
            }
            catch (FormatException)
            {
                result = IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken());
            }

            if (!result.Succeeded)
            {
                return CreateValidationProblem(result);
            }

            return TypedResults.Ok();
        });

        var accountGroup = endpoints.MapGroup("account")
            .WithTags("Account")
            .RequireAuthorization();

        accountGroup.MapPost("/2fa", async Task<Results<Ok<TwoFactorResponse>, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, [FromBody] TwoFactorRequest tfaRequest, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<ApplicationUser>>();
            var userManager = signInManager.UserManager;
            if (await userManager.GetUserAsync(claimsPrincipal) is not { } user)
            {
                return TypedResults.NotFound();
            }

            if (tfaRequest.Enable == true)
            {
                if (tfaRequest.ResetSharedKey)
                {
                    return CreateValidationProblem("CannotResetSharedKeyAndEnable",
                        "Resetting the 2fa shared key must disable 2fa until a 2fa token based on the new shared key is validated.");
                }

                if (string.IsNullOrEmpty(tfaRequest.TwoFactorCode))
                {
                    return CreateValidationProblem("RequiresTwoFactor",
                        "No 2fa token was provided by the request. A valid 2fa token is required to enable 2fa.");
                }

                if (!await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, tfaRequest.TwoFactorCode))
                {
                    return CreateValidationProblem("InvalidTwoFactorCode",
                        "The 2fa token provided by the request was invalid. A valid 2fa token is required to enable 2fa.");
                }

                await userManager.SetTwoFactorEnabledAsync(user, true);
            }
            else if (tfaRequest.Enable == false || tfaRequest.ResetSharedKey)
            {
                await userManager.SetTwoFactorEnabledAsync(user, false);
            }

            if (tfaRequest.ResetSharedKey)
            {
                await userManager.ResetAuthenticatorKeyAsync(user);
            }

            string[]? recoveryCodes = null;
            if (tfaRequest.ResetRecoveryCodes || tfaRequest.Enable == true && await userManager.CountRecoveryCodesAsync(user) == 0)
            {
                var recoveryCodesEnumerable = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                recoveryCodes = recoveryCodesEnumerable?.ToArray();
            }

            if (tfaRequest.ForgetMachine)
            {
                await signInManager.ForgetTwoFactorClientAsync();
            }

            var key = await userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await userManager.ResetAuthenticatorKeyAsync(user);
                key = await userManager.GetAuthenticatorKeyAsync(user);

                if (string.IsNullOrEmpty(key))
                {
                    throw new NotSupportedException("The user manager must produce an authenticator key after reset.");
                }
            }

            return TypedResults.Ok(new TwoFactorResponse
            {
                SharedKey = key,
                RecoveryCodes = recoveryCodes,
                RecoveryCodesLeft = recoveryCodes?.Length ?? await userManager.CountRecoveryCodesAsync(user),
                IsTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
                IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user),
            });
        });

        accountGroup.MapGet("/info", async Task<Results<Ok<InfoResponse>, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not { } user)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(await CreateInfoResponseAsync(user, userManager));
        });

        accountGroup.MapPost("/info", async Task<Results<Ok<InfoResponse>, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, [FromBody] InfoRequest infoRequest, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not { } user)
            {
                return TypedResults.NotFound();
            }

            if (!string.IsNullOrEmpty(infoRequest.NewEmail) && !_emailAddressAttribute.IsValid(infoRequest.NewEmail))
            {
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(infoRequest.NewEmail)));
            }

            if (!string.IsNullOrEmpty(infoRequest.NewPassword))
            {
                if (string.IsNullOrEmpty(infoRequest.OldPassword))
                {
                    return CreateValidationProblem("OldPasswordRequired",
                        "The old password is required to set a new password. If the old password is forgotten, use /resetPassword.");
                }

                var changePasswordResult = await userManager.ChangePasswordAsync(user, infoRequest.OldPassword, infoRequest.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    return CreateValidationProblem(changePasswordResult);
                }
            }

            if (!string.IsNullOrEmpty(infoRequest.NewEmail))
            {
                var email = await userManager.GetEmailAsync(user);

                if (email != infoRequest.NewEmail)
                {
                    await SendConfirmationEmailAsync(user, userManager, context, infoRequest.NewEmail, isChange: true);
                }
            }

            return TypedResults.Ok(await CreateInfoResponseAsync(user, userManager));
        });

        #region CustomCode
        // Endpoint to fetch all active sessions for the current user. This is useful for account management pages where users can see all their active sessions and log out of them if needed.
        accountGroup.MapGet("/sessions", async Task<Results<Ok<SessionsModel>, UnauthorizedHttpResult, ProblemHttpResult>> (
            ClaimsPrincipal claimsPrincipal,
            [FromServices] IUserSessionManager sessionManager) =>
        {
            var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return TypedResults.Unauthorized();
            }

            var sessions = await sessionManager.GetAll(long.Parse(userId));

            return TypedResults.Ok(new SessionsModel { Sessions = sessions });
        }).WithSummary("Retrieves all active sessions for the current user.");

        accountGroup.MapDelete("/sessions/{sessionIdHash}", async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> (
            string sessionIdHash,
            ClaimsPrincipal claimsPrincipal,
            [FromServices] IUserSessionManager sessionManager) =>
        {
            var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return TypedResults.Unauthorized();
            }

            await sessionManager.Remove(
                long.Parse(userId),
                sessionIdHash);

            return TypedResults.NoContent();
        }).WithSummary("Revokes the specified session for the current user.");

        accountGroup.MapPost("/revoke-sessions", async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> (
            ClaimsPrincipal claimsPrincipal,
            [FromServices] IUserSessionManager sessionManager) =>
        {
            var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return TypedResults.Unauthorized();
            }

            await sessionManager.RemoveAll(long.Parse(userId));

            return TypedResults.NoContent();
        }).WithSummary("Revokes all active sessions for the current user.");

        // Fetch AI setting for the current user.
        accountGroup.MapGet("/ai-setting", async Task<Results<Ok<AISettingDto>, NoContent, UnauthorizedHttpResult, ProblemHttpResult>> (
            ClaimsPrincipal claimsPrincipal,
            [FromServices] IUserRepository userRepository) =>
        {
            var userId = claimsPrincipal.GetUserId();

            var dto = await userRepository.GetAISetting(userId);

            if (dto is null) { return TypedResults.NoContent(); }

            return TypedResults.Ok(dto);
        }).WithSummary("Retrieve AI setting for the current user.")
        .WithDescription("Returns 204 No Content if the user has not configured AI setting.");

        // Update AI setting for the current user.
        accountGroup.MapPut("/ai-setting", async Task<Results<Ok<AISettingDto>, ValidationProblem, UnauthorizedHttpResult, BadRequest<string>>> (
            ClaimsPrincipal claimsPrincipal,
            [FromBody] CreateUpdateAISettingModel input,
            IChatClientFactory chatClientFactory,
            IDataProtector dataProtector,
            TimeProvider timeProvider,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(input.Key)
                && input.Provider != (int)AIProvider.Ollama)
            {
                return CreateValidationProblem(
                    nameof(input.Key),
                    $"Required when {nameof(input.Provider)} is {input.Provider}");
            }

            await chatClientFactory.Verify(new AISettingDto(
                Guid.NewGuid(),
                input.CustomEndpoint,
                input.Key,
                input.Model,
                (AIProvider)input.Provider,
                timeProvider.GetUtcNow(),
                null),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(input.Key))
            {
                // Encrypt key
                input = input with
                {
                    Key = input.Key.Protect(
                        dataProtector, 
                        Constants.SecretProtectorPurpose)
                };
            }

            var userId = claimsPrincipal.GetUserId();

            var dto = await userService.CreateUpdateAISetting(
                userId,
                input,
                cancellationToken);

            return TypedResults.Ok(dto);
        }).WithValidation<CreateUpdateAISettingModel>()
        .WithSummary("Update AI setting and API key for the current user.")
        .WithDescription("The Key is encrypted before storage. For Ollama, the Key can be omitted.");
        #endregion

        async Task SendConfirmationEmailAsync(TUser user, UserManager<TUser> userManager, HttpContext context, string email, bool isChange = false)
        {
            if (confirmEmailEndpointName is null)
            {
                throw new NotSupportedException("No email confirmation endpoint was registered!");
            }

            var code = isChange
                ? await userManager.GenerateChangeEmailTokenAsync(user, email)
                : await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var userId = await userManager.GetUserIdAsync(user);
            var routeValues = new RouteValueDictionary()
            {
                ["userId"] = userId,
                ["code"] = code,
            };

            if (isChange)
            {
                // This is validated by the /confirmEmail endpoint on change.
                routeValues.Add("changedEmail", email);
            }

            var confirmEmailUrl = linkGenerator.GetUriByName(context, confirmEmailEndpointName, routeValues)
                ?? throw new NotSupportedException($"Could not find endpoint named '{confirmEmailEndpointName}'.");

            await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
        }

        return new IdentityEndpointsConventionBuilder(endpoints as RouteGroupBuilder ?? throw new InvalidOperationException());
    }

    private static ValidationProblem CreateValidationProblem(string errorCode, string errorDescription) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> {
            { errorCode, [errorDescription] }
        });

    private static ValidationProblem CreateValidationProblem(IdentityResult result)
    {
        // We expect a single error code and description in the normal case.
        // This could be golfed with GroupBy and ToDictionary, but perf! :P
        Debug.Assert(!result.Succeeded);
        var errorDictionary = new Dictionary<string, string[]>(1);

        foreach (var error in result.Errors)
        {
            string[] newDescriptions;

            if (errorDictionary.TryGetValue(error.Code, out var descriptions))
            {
                newDescriptions = new string[descriptions.Length + 1];
                Array.Copy(descriptions, newDescriptions, descriptions.Length);
                newDescriptions[descriptions.Length] = error.Description;
            }
            else
            {
                newDescriptions = [error.Description];
            }

            errorDictionary[error.Code] = newDescriptions;
        }

        return TypedResults.ValidationProblem(errorDictionary);
    }

    private static async Task<InfoResponse> CreateInfoResponseAsync<TUser>(TUser user, UserManager<TUser> userManager)
        where TUser : class
    {
        return new()
        {
            Email = await userManager.GetEmailAsync(user) ?? throw new NotSupportedException("Users must have an email."),
            IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user),
        };
    }

    // Wrap RouteGroupBuilder with a non-public type to avoid a potential future behavioral breaking change.
    private sealed class IdentityEndpointsConventionBuilder(RouteGroupBuilder inner) : IEndpointConventionBuilder
    {
        private IEndpointConventionBuilder InnerAsConventionBuilder => inner;

        public void Add(Action<EndpointBuilder> convention) => InnerAsConventionBuilder.Add(convention);
        public void Finally(Action<EndpointBuilder> finallyConvention) => InnerAsConventionBuilder.Finally(finallyConvention);
    }

    #region CustomCode
    private static async Task CreateDefaultTeam<TUser>(
        TUser user, 
        IServiceProvider sp) where TUser : ApplicationUser, new()
    {
        var teamService = sp.GetRequiredService<ITeamService>();
        var timeProvider = sp.GetRequiredService<TimeProvider>();
        var today = timeProvider.GetUtcNow().Date;
        var random = new Random();
        var players = new List<CreatePlayerDto>();
        players.AddRange(Enumerable.Range(1, 3).Select(index => new CreatePlayerDto()
        {
            DateOfBirth = GetRandomDateOfBirth(today, random),
            Type = (int)PlayerType.Goalkeeper,
            Value = Domain.Constants.InitialPlayerValue
        }));

        players.AddRange(Enumerable.Range(1, 6).Select(index => new CreatePlayerDto()
        {
            DateOfBirth = GetRandomDateOfBirth(today, random),
            Type = (int)PlayerType.Defender,
            Value = Domain.Constants.InitialPlayerValue
        }));

        players.AddRange(Enumerable.Range(1, 6).Select(index => new CreatePlayerDto()
        {
            DateOfBirth = GetRandomDateOfBirth(today, random),
            Type = (int)PlayerType.Midfielder,
            Value = Domain.Constants.InitialPlayerValue
        }));

        players.AddRange(Enumerable.Range(1, 5).Select(index => new CreatePlayerDto()
        {
            DateOfBirth = GetRandomDateOfBirth(today, random),
            Type = (int)PlayerType.Attacker,
            Value = Domain.Constants.InitialPlayerValue
        }));

        await teamService.Create(user,
            new CreateTeamDto
            {
                TransferBudget = Domain.Constants.InitialTeamTransferBudget,
            },
            players);
    }

    private static DateOnly GetRandomDateOfBirth(
        DateTime today,
        Random random)
    {
        var latestPossibleDOB = today.AddYears(-Domain.Constants.MinPlayerAge);

        // Exact date for someone who turns Max + 1 today
        var boundaryDate = today.AddYears(-Domain.Constants.MaxPlayerAge - 1);
        // One day after their Max + 1 birthday is the earliest they can be born to be Max
        var earliestPossibleDOB = boundaryDate.AddDays(1);

        // Calculate total days span
        int rangeInDays = (latestPossibleDOB - earliestPossibleDOB).Days;

        // Generate random DOB
        var randomDOB = earliestPossibleDOB.AddDays(random.Next(rangeInDays + 1));

        return DateOnly.FromDateTime(randomDOB);
    }
    #endregion

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromBodyAttribute : Attribute, IFromBodyMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromServicesAttribute : Attribute, IFromServiceMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromQueryAttribute : Attribute, IFromQueryMetadata
    {
        public string? Name => null;
    }
}

