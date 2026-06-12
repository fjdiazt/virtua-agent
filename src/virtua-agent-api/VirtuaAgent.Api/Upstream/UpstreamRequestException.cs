using System.Net;

namespace VirtuaAgent.Upstream;

public sealed class UpstreamRequestException : Exception
{
    public UpstreamRequestException(
        HttpStatusCode statusCode,
        string message,
        string? upstreamErrorType,
        string? upstreamErrorCode,
        string responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        UpstreamErrorType = upstreamErrorType;
        UpstreamErrorCode = upstreamErrorCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }
    public string? UpstreamErrorType { get; }
    public string? UpstreamErrorCode { get; }
    public string ResponseBody { get; }
}
