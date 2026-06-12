using VirtuaAgent.OpenAi;
using System.Text;

namespace VirtuaAgent.Upstream;

public interface IOpenAiCompatibleUpstreamClient
{
    Task<ModelListResponse> ListModelsAsync(CancellationToken cancellationToken = default);
    Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
    Task StreamChatAsync(ChatCompletionRequest request, Stream output, CancellationToken cancellationToken = default);
    async Task StreamChatAsync(ChatCompletionRequest request, Func<string, CancellationToken, Task> onDataAsync, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await StreamChatAsync(request, stream, cancellationToken);
        var text = Encoding.UTF8.GetString(stream.ToArray());
        foreach (var eventBlock in text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var data = string.Join('\n', eventBlock
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Where(line => line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                .Select(line => line[5..].TrimStart()));
            if (!string.IsNullOrWhiteSpace(data))
            {
                await onDataAsync(data, cancellationToken);
            }
        }
    }
}
