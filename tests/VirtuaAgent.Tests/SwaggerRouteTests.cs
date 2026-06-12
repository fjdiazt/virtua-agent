using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VirtuaAgent.Tests;

public sealed class SwaggerRouteTests
{
    [Fact]
    public async Task SwaggerJsonIncludesOpenAiCompatibleEndpoint()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"/v1/chat/completions\"", json);
        Assert.Contains("\"/v1/models\"", json);
        Assert.Contains("CreateChatCompletion", json);
    }
}
