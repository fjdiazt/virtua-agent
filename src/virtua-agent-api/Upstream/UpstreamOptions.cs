namespace VirtuaAgent.Upstream;

public sealed record UpstreamOptions
{
    public string BaseUrl { get; init; } = "http://localhost:8080";
    public int RequestTimeoutSeconds { get; init; } = 600;
    public int ModelListTimeoutSeconds { get; init; } = 30;
}
