using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VirtuaAgent.OpenAi;
using Microsoft.Extensions.Options;

namespace VirtuaAgent.Upstream;

public sealed class OpenAiCompatibleUpstreamClient : IOpenAiCompatibleUpstreamClient
{
    private readonly HttpClient _httpClient;
    private readonly UpstreamOptions _options;

    public OpenAiCompatibleUpstreamClient(HttpClient httpClient, IOptions<UpstreamOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ModelListResponse> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ModelListTimeoutSeconds)));

        using var response = await _httpClient.GetAsync("/v1/models", timeout.Token);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ModelListResponse>(JsonOptions.Default, cancellationToken))!;
    }

    public async Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, JsonOptions.Default, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions.Default, cancellationToken))!;
    }

    public async Task StreamChatAsync(ChatCompletionRequest request, Stream output, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, JsonOptions.Default, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var upstreamStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await upstreamStream.CopyToAsync(output, cancellationToken);
    }

    public async Task StreamChatAsync(ChatCompletionRequest request, Func<string, CancellationToken, Task> onDataAsync, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions.Default), Encoding.UTF8, "application/json")
        };
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var dataLines = new List<string>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                if (dataLines.Count > 0)
                {
                    await onDataAsync(string.Join('\n', dataLines), cancellationToken);
                    dataLines.Clear();
                }

                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                dataLines.Add(line[5..].TrimStart());
            }
        }

        if (dataLines.Count > 0)
        {
            await onDataAsync(string.Join('\n', dataLines), cancellationToken);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = TryReadOpenAiError(body);
        var message = error?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"Upstream returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
        }

        throw new UpstreamRequestException(
            response.StatusCode,
            message,
            error?.Type,
            error?.Code,
            body);
    }

    private static UpstreamError? TryReadOpenAiError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var errorElement))
            {
                return null;
            }

            return new UpstreamError(
                ReadString(errorElement, "message"),
                ReadString(errorElement, "type"),
                ReadString(errorElement, "code"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private sealed record UpstreamError(string? Message, string? Type, string? Code);
}
