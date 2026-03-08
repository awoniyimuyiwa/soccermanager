using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Models;
using Api.Extensions;
using Application.Contracts;
using Azure;
using Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;

namespace Api.Services;

/// <summary>
/// Implementation of <see cref="IChatClientFactory"/>
/// This factory is placed in the API layer due to its direct dependency on ASP.NET Core Data Protection abstractions.
/// </summary>
/// <param name="dataProtector">
/// Provides cryptographic services, ensuring 
/// LLM API keys are isolated from other encrypted data in the system.
/// </param>
/// <param name="httpClientFactory">
/// Manages the lifecycle of pooled HttpClient instances to prevent socket exhaustion 
/// and allow provider-specific configurations (e.g., custom timeouts for local LLMs).
/// </param>
/// <param name="userRepository">
/// Accesses the persistence layer to retrieve user-specific AI settings, 
/// provider types, and encrypted keys from the database.
/// </param>
public class ChatClientFactory(
     IDataProtector dataProtector,
     IHttpClientFactory httpClientFactory,
     IUserRepository userRepository) : IChatClientFactory
{
    readonly Lazy<IDataProtector> _dataProtector = new(() => dataProtector.CreateProtector(Constants.SecretProtectorPurpose));
    readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    readonly IUserRepository _userRepository = userRepository;
   
    public async Task <IChatClient> Create(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var setting = await _userRepository.GetAISetting(
            userId, 
            cancellationToken) ?? throw new DomainException("AI setting has not ben configured");

        setting = setting with
        {
            Key = setting.Key?.Unprotect(_dataProtector.Value, null)
        };

        return setting.Provider switch
        {
            AIProvider.Anthropic => new AnthropicClient(new ClientOptions
            {
                ApiKey = setting.Key,
                HttpClient = _httpClientFactory.CreateClient(Constants.LlmHttpClientName)
            }).AsIChatClient(setting.Model),

            AIProvider provider when provider is AIProvider.OpenAI
            or AIProvider.Gemini
            or AIProvider.Groq   
            or AIProvider.Ollama
            => CreateOpenAICompatibleChatClient(setting),

            _ => throw new NotSupportedException($"{setting.Provider} is not supported.")
        };
    }

    public async Task Verify(
        AISettingDto setting,
        CancellationToken cancellationToken = default)
    {
        // Keep verification short to ensure the UI remains responsive during the "Save" operation.
        var timeout = TimeSpan.FromSeconds(5);

        // The await ensures the Task returned by the switch is executed
        await (setting.Provider switch
        {
            AIProvider.Anthropic => VerifyAnthropic(
                setting,
                timeout,
                cancellationToken),

            AIProvider provider when provider is AIProvider.OpenAI
                or AIProvider.Gemini
                or AIProvider.Groq
                or AIProvider.Ollama
                => VerifyOpenAI(
                    setting, 
                    timeout,
                    cancellationToken),

            _ => throw new NotSupportedException($"{setting.Provider} is not supported.")
        });
    }

    private async Task VerifyAnthropic(
        AISettingDto setting, 
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var anthropic = new AnthropicClient(new ClientOptions
        {
            ApiKey = setting.Key,
            HttpClient = _httpClientFactory.CreateClient(Constants.LlmHttpClientName),
            Timeout = timeout
        });

        try
        {
            var modelInfo = await anthropic.Models.Retrieve(
                setting.Model,
                cancellationToken: cancellationToken);
        }
        catch (AnthropicApiException apiEx) // Specific API errors (4xx, 5xx)
        {
            if (apiEx.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new DomainException("Anthropic API Key is invalid or expired.");
            }

            if (apiEx.StatusCode == HttpStatusCode.NotFound)
            {
                // Fallback to List for suggestions
                var firstPage = await anthropic.Models.List(cancellationToken: cancellationToken);
                var suggestions = string.Join(", ", firstPage.Items.Take(3).Select(m => m.ID));

                throw new DomainException($"Model '{setting.Model}' not found. Examples: {suggestions}");
            }

            throw new DomainException($"Anthropic API Error: {apiEx.StatusCode}");
        }
        catch (AnthropicException ex) // Base SDK errors
        {
            throw new DomainException($"Anthropic SDK Error: {ex.Message}");
        }
        catch (Exception ex) // Connection/Timeout errors
        {
            throw new DomainException($"Network error connecting to Anthropic: {ex.Message}");
        }
    }

    private async Task VerifyOpenAI(
        AISettingDto setting,
        TimeSpan timeOut,
        CancellationToken cancellationToken)
    {
        var client = CreateBaseOpenAIClient(
            setting, 
            timeOut);

        try
        {
            var modelClient = client.GetOpenAIModelClient();

            var modelResponse = await modelClient.GetModelAsync(
                setting.Model, 
                cancellationToken);
        }
        catch (ClientResultException ex) when (ex.Status == 401)
        {
            throw new DomainException($"{setting.Provider} API Key is invalid.");
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            // Fallback: Fetch a few examples to help the user
            var modelClient = client.GetOpenAIModelClient();
            try
            {
                // ONLY try to get suggestions if we suspect the ENDPOINT is actually valid
                // If this also 404s, it means the URL is wrong, not just the model name.
                var models = await modelClient.GetModelsAsync(cancellationToken);
                var suggestions = string.Join(", ", models.Value.Take(3).Select(m => m.Id));
                throw new DomainException($"Model '{setting.Model}' not found. Examples: {suggestions}");
            }
            catch
            {
                // If the second call fails, the problem is the Custom Endpoint URL
                throw new DomainException($"Could not reach {setting.Provider} at '{setting.CustomEndpoint}'. Verify the URL.");
            }
        }
        catch (Exception ex)
        {
            // Handles Network, Timeouts, and DNS issues (crucial for Ollama)
            throw new DomainException($"{setting.Provider} connection failed: {ex.Message}");
        }
    }

    private IChatClient CreateOpenAICompatibleChatClient(AISettingDto setting)
    {
        return CreateBaseOpenAIClient(setting)
            .GetChatClient(setting.Model)
            .AsIChatClient();
    }

    private OpenAIClient CreateBaseOpenAIClient(
        AISettingDto setting,
        TimeSpan? timeout = null)
    {
        var defaultEndpoint = setting.Provider switch
        {
            AIProvider.OpenAI => null,
            AIProvider.Gemini => "https://generativelanguage.googleapis.com",
            AIProvider.Groq => "https://api.groq.com",
            AIProvider.Ollama => "http://localhost:11434/v1",
            _ => throw new InvalidOperationException()
        };

        var httpClient = _httpClientFactory.CreateClient(Constants.LlmHttpClientName);
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient),
            NetworkTimeout = timeout
        };

        var endpoint = setting.CustomEndpoint ?? defaultEndpoint;
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            options.Endpoint = new Uri(endpoint);
        }

        // Determine the key: Ollama gets "nopass", others get the decrypted key.
        // Add a final fallback to "ignored" to prevent the SDK from crashing on a null.
        var credentialKey = setting.Provider == AIProvider.Ollama
            ? "nopass"
            : setting.Key ?? "ignored";

        // Initialize with the safe key
        return new OpenAIClient(new ApiKeyCredential(credentialKey), options);
    }
}

