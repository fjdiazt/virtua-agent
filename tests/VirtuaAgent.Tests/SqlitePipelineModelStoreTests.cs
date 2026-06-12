using VirtuaAgent.PipelineModels;

namespace VirtuaAgent.Tests;

public sealed class SqlitePipelineModelStoreTests
{
    [Fact]
    public async Task InitializeAsyncCreatesPipelineTestFixture()
    {
        var store = new SqlitePipelineModelStore("Data Source=:memory:");
        await store.InitializeAsync();

        var model = await store.GetAsync("virtua-agent-test");

        Assert.NotNull(model);
        Assert.Equal("virtua-agent", model!.OwnedBy);
        Assert.Equal("gemma-4-26B-A4B-it-uncensored-vision", model.Pipeline!.DefaultModel);
        Assert.Equal(0.2, model.Pipeline.DefaultTemperature);
        Assert.Equal(3, model.Pipeline.Stages.Count);
        Assert.Equal("Draft", model.Pipeline.Stages[0].Name);
        Assert.Equal("Tighten", model.Pipeline.Stages[1].Name);
        Assert.Equal("Apply rules", model.Pipeline.Stages[2].Name);
        Assert.Equal(1, model.Pipeline.Stages[0].Repeat);
        Assert.Equal(2, model.Pipeline.Stages[1].Repeat);
        Assert.Equal(1, model.Pipeline.Stages[2].Repeat);
        Assert.Contains("[draft]", model.Pipeline.Stages[0].Instructions);
        Assert.Contains("[tightened-iterated]", model.Pipeline.Stages[1].Instructions);
        Assert.Contains("exactly 2 bullets", model.Pipeline.Stages[2].Instructions);
        Assert.Contains("[rules-applied]", model.Pipeline.Stages[2].Instructions);
    }

    [Fact]
    public async Task SavesListsGetsAndDeletesPipelineModels()
    {
        var store = new SqlitePipelineModelStore("Data Source=:memory:");
        await store.InitializeAsync();
        var model = new PipelineModelDefinition
        {
            Id = "virtua-agent/editor",
            OwnedBy = "virtua-agent",
            Pipeline = new PipelineRequestDto
            {
                DefaultModel = "local-model",
                DefaultTemperature = 0.2,
                DefaultMaxTokens = 128,
                Stages =
                [
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Name = "Corrector",
                        Instructions = "Correct spelling.",
                        Agent = new AgentRequestDto { Model = "local-model" }
                    }
                ]
            }
        };

        await store.SaveAsync(model);
        var listed = (await store.ListAsync())
            .Where(saved => saved.Id == "virtua-agent/editor")
            .ToList();
        var loaded = await store.GetAsync("virtua-agent/editor");
        var deleted = await store.DeleteAsync("virtua-agent/editor");
        var afterDelete = (await store.ListAsync())
            .Where(saved => saved.Id == "virtua-agent/editor")
            .ToList();

        Assert.Single(listed);
        Assert.Equal("virtua-agent/editor", listed[0].Id);
        Assert.NotNull(loaded);
        Assert.Equal("local-model", loaded!.Pipeline!.DefaultModel);
        Assert.Equal(0.2, loaded.Pipeline.DefaultTemperature);
        Assert.Equal(128, loaded.Pipeline.DefaultMaxTokens);
        Assert.Equal("Corrector", loaded.Pipeline.Stages[0].Name);
        Assert.Equal("Correct spelling.", loaded.Pipeline.Stages[0].Instructions);
        Assert.True(deleted);
        Assert.Empty(afterDelete);
    }
}
