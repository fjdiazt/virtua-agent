using VirtuaAgent.OpenAi;

namespace VirtuaAgent.Orchestration;

public static class PipelineStagePromptComposer
{
    // Keep protocol and data blocks in a user message for maximum upstream compatibility.
    // If model compliance requires stronger framing, revisit moving protocol or selected
    // blocks to system messages as a separate behavior change.
    public static List<ChatMessageDto> Compose(
        PipelineContext context,
        PipelineStageDefinition stage,
        int executionIndex,
        string? protocol)
    {
        if (executionIndex == 0
            && stage.Input is null
            && string.IsNullOrWhiteSpace(protocol)
            && string.IsNullOrWhiteSpace(stage.Instructions))
        {
            return new List<ChatMessageDto>(context.OriginalMessages);
        }

        var input = PipelineStageInputDefinition.Resolve(stage.Input, executionIndex);
        var effectiveProtocol = string.IsNullOrWhiteSpace(protocol)
            ? PipelinePromptProtocol.Default.Instructions.Trim()
            : protocol.Trim();

        var messages = new List<ChatMessageDto>();
        var textSections = new List<string>();

        if (input.OriginalMessages == "full")
        {
            messages.AddRange(context.OriginalMessages);
        }
        else if (input.OriginalMessages == "text")
        {
            textSections.Add("Original conversation:");
            textSections.Add(FormatConversation(context.OriginalMessages));
        }

        if (!string.IsNullOrWhiteSpace(effectiveProtocol))
        {
            textSections.Add("Pipeline protocol:");
            textSections.Add(effectiveProtocol);
        }

        if (input.PriorStageOutput == "last")
        {
            var label = string.IsNullOrWhiteSpace(context.CurrentAnswerLabel)
                ? "previous stage"
                : context.CurrentAnswerLabel!;
            textSections.Add($"Prior stage output from \"{label}\":");
            textSections.Add(string.IsNullOrWhiteSpace(context.CurrentAnswer) ? "[empty]" : context.CurrentAnswer!);
        }

        if (!string.IsNullOrWhiteSpace(stage.Instructions))
        {
            textSections.Add("Stage instruction:");
            textSections.Add(stage.Instructions);
        }

        if (textSections.Count > 0)
        {
            messages.Add(new ChatMessageDto
            {
                Role = "user",
                Content = string.Join("\n\n", textSections)
            });
        }

        if (messages.Count == 0)
        {
            messages.Add(new ChatMessageDto { Role = "user", Content = "" });
        }

        return messages;
    }

    private static string FormatConversation(IEnumerable<ChatMessageDto> messages) =>
        string.Join("\n", messages.Select(message => $"{message.Role}: {message.Content.AsText()}"));
}
