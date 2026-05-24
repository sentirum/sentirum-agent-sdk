using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Sentirum.Agent.Providers.MiniMax;

/// <summary>
/// Raw HTTP <see cref="IChatClient"/> for the MiniMax Chat Completions API.
/// Supports <c>reasoning_split</c>, streaming, tool calling.
/// </summary>
public sealed class MiniMaxChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly bool _reasoningSplit;
    private readonly ILogger? _logger;
    private readonly Uri _endpoint;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public MiniMaxChatClient(
        string apiKey,
        string model,
        string? baseUrl = null,
        bool reasoningSplit = true,
        HttpClient? httpClient = null,
        ILogger<MiniMaxChatClient>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _model = model;
        _reasoningSplit = reasoningSplit;
        _logger = logger;

        var baseStr = baseUrl ?? MiniMaxDefaults.OpenAIBaseUrl;
        _endpoint = new Uri(baseStr.TrimEnd('/') + "/chat/completions");

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc />
    public ChatClientMetadata Metadata => new("MiniMax", _endpoint, _model);

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var body = BuildRequestBody(messages, options, stream: false);
        var json = JsonSerializer.Serialize(body, _jsonOptions);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpResponse = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MiniMax API error: {httpResponse.StatusCode} — {responseBody}");
        }

        var response = JsonSerializer.Deserialize<JsonElement>(responseBody);
        return ParseResponse(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var body = BuildRequestBody(messages, options, stream: true);
        var json = JsonSerializer.Serialize(body, _jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        using var httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data: ".Length..];
            if (data == "[DONE]")
            {
                break;
            }

            var chunk = JsonSerializer.Deserialize<JsonElement>(data);
            if (!chunk.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                continue;
            }

            var delta = choices[0].GetProperty("delta");

            // Reasoning content (separate from main content)
            if (delta.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String)
            {
                var reasoning = rc.GetString();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, reasoning)
                    {
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            ["MiniMax.IsReasoning"] = true,
                        },
                    };
                }
            }

            // Main content
            if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            {
                var text = c.GetString();
                if (text is not null)
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, text);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType == typeof(ChatClientMetadata) ? Metadata : null;
    }

    // ------------------------------------------------------------------
    // Request
    // ------------------------------------------------------------------

    private Dictionary<string, object?> BuildRequestBody(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        bool stream)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = options?.ModelId ?? _model,
            ["messages"] = SerializeMessages(messages),
            ["stream"] = stream,
        };

        if (_reasoningSplit)
        {
            body["reasoning_split"] = true;
        }

        if (options?.MaxOutputTokens is int maxTokens)
        {
            body["max_completion_tokens"] = maxTokens;
        }

        if (options?.Temperature is float temp)
        {
            body["temperature"] = temp;
        }

        if (options?.TopP is float topP)
        {
            body["top_p"] = topP;
        }

        if (options?.Tools is { Count: > 0 })
        {
            body["tools"] = SerializeTools(options.Tools);
        }

        return body;
    }

    private static List<Dictionary<string, object?>> SerializeMessages(IEnumerable<ChatMessage> messages)
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (var msg in messages)
        {
            var role = msg.Role.Value switch
            {
                "system" => "system",
                "user" => "user",
                "assistant" => "assistant",
                "tool" => "tool",
                _ => "user",
            };

            var entry = new Dictionary<string, object?> { ["role"] = role };

            var toolCalls = msg.Contents?.OfType<FunctionCallContent>().ToList();
            if (toolCalls is { Count: > 0 })
            {
                entry["tool_calls"] = toolCalls.Select((tc, i) => new Dictionary<string, object?>
                {
                    ["id"] = tc.CallId ?? $"call_{i}",
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Arguments is not null
                            ? string.Join(",", tc.Arguments.Select(kv => $"\"{kv.Key}\":{JsonSerializer.Serialize(kv.Value)}"))
                            : "{}",
                    },
                }).ToList();
            }
            else
            {
                entry["content"] = msg.Text;
            }

            result.Add(entry);
        }
        return result;
    }

    private static List<Dictionary<string, object?>> SerializeTools(IList<AITool> tools)
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (var tool in tools)
        {
            if (tool is AIFunction af)
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = af.Name,
                        ["description"] = af.Description,
                        ["parameters"] = af.JsonSchema,
                    },
                });
            }
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Response
    // ------------------------------------------------------------------

    private static ChatResponse ParseResponse(JsonElement response)
    {
        var messages = new List<ChatMessage>();

        if (!response.TryGetProperty("choices", out var choices))
        {
            return new ChatResponse(messages);
        }

        foreach (var choice in choices.EnumerateArray())
        {
            var msg = choice.GetProperty("message");
            var content = TryGetString(msg, "content");
            var role = TryGetString(msg, "role") ?? "assistant";

            // Check for tool calls first
            if (msg.TryGetProperty("tool_calls", out var toolCalls))
            {
                var fcList = new List<AIContent>();
                var tcList = toolCalls.EnumerateArray().ToList();
                for (var i = 0; i < tcList.Count; i++)
                {
                    var tc = tcList[i];
                    var fn = tc.GetProperty("function");
                    var args = TryGetString(fn, "arguments") ?? "{}";
                    var argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(args);
                    fcList.Add(new FunctionCallContent(
                        TryGetString(tc, "id") ?? $"call_{i}",
                        fn.GetProperty("name").GetString()!,
                        argsDict));
                }

                var toolMessage = new ChatMessage(new ChatRole(role), fcList);
                if (content is not null)
                {
                    toolMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    toolMessage.AdditionalProperties["MiniMax.Content"] = content;
                }

                messages.Add(toolMessage);
                continue;
            }

            // Regular message
            var chatMessage = new ChatMessage(new ChatRole(role), content ?? string.Empty);

            // Reasoning details
            if (msg.TryGetProperty("reasoning_details", out var rd) && rd.GetArrayLength() > 0)
            {
                var reasoningText = TryGetString(rd[0], "text");
                if (reasoningText is not null)
                {
                    chatMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    chatMessage.AdditionalProperties["MiniMax.Reasoning"] = reasoningText;
                }
            }

            messages.Add(chatMessage);
        }

        // Usage
        ChatResponse chatResponse = new(messages);

        if (response.TryGetProperty("usage", out var usageEl))
        {
            chatResponse.Usage = new()
            {
                InputTokenCount = TryGetInt(usageEl, "prompt_tokens"),
                OutputTokenCount = TryGetInt(usageEl, "completion_tokens"),
            };
        }

        return chatResponse;
    }

    private static string? TryGetString(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }

    private static int TryGetInt(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;
    }
}
