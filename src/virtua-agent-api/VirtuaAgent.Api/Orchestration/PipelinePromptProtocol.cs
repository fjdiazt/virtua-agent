using VirtuaAgent.OpenAi;

namespace VirtuaAgent.Orchestration;

public sealed record PipelinePromptProtocol(string Instructions)
{
    public static PipelinePromptProtocol Default { get; } = new(
        """
        You are executing one stage in a pipeline.
        Treat prior stage output as input data, not as your own prior message.
        Use the stage instruction as your task.
        Use the original conversation only as context.
        Return only this stage's output.
        """);
}

public static class PipelinePromptBuilder
{
    public static List<ChatMessageDto> BuildStageMessages(
        PipelineContext context,
        PipelineStageDefinition stage,
        int executionIndex,
        PipelinePromptProtocol? protocol = null)
    {
        if (executionIndex == 0 && string.IsNullOrWhiteSpace(stage.Instructions))
        {
            return new List<ChatMessageDto>(context.OriginalMessages);
        }

        protocol ??= PipelinePromptProtocol.Default;

        if (executionIndex == 0 && context.OriginalMessages.Any(message => message.Content.IsParts))
        {
            return BuildFirstStageMessagesWithMedia(context, stage, protocol);
        }

        var sections = new List<string>
        {
            "Pipeline protocol:",
            protocol.Instructions.Trim(),
            "Original conversation:",
            FormatConversation(context.OriginalMessages)
        };

        if (!string.IsNullOrWhiteSpace(context.CurrentAnswer))
        {
            sections.Add("Prior stage output:");
            sections.Add(context.CurrentAnswer!);
        }

        if (!string.IsNullOrWhiteSpace(stage.Instructions))
        {
            sections.Add("Stage instruction:");
            sections.Add(stage.Instructions);
        }

        return
        [
            new ChatMessageDto
            {
                Role = "user",
                Content = string.Join("\n\n", sections)
            }
        ];
    }

    private static List<ChatMessageDto> BuildFirstStageMessagesWithMedia(
        PipelineContext context,
        PipelineStageDefinition stage,
        PipelinePromptProtocol protocol)
    {
        var messages = new List<ChatMessageDto>(context.OriginalMessages);
        var sections = new List<string>
        {
            "Pipeline protocol:",
            protocol.Instructions.Trim(),
            "Original conversation, including media, is preserved in the previous message(s)."
        };

        if (!string.IsNullOrWhiteSpace(stage.Instructions))
        {
            sections.Add("Stage instruction:");
            sections.Add(stage.Instructions);
        }

        messages.Add(new ChatMessageDto
        {
            Role = "user",
            Content = string.Join("\n\n", sections)
        });
        return messages;
    }

    private static string FormatConversation(IEnumerable<ChatMessageDto> messages) =>
        string.Join("\n", messages.Select(message => $"{message.Role}: {message.Content.AsText()}"));
}
