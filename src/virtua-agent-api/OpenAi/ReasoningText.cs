namespace VirtuaAgent.OpenAi;

public static class ReasoningText
{
    public static string StripThinkTags(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var extractor = new ThinkTagStreamExtractor();
        var result = extractor.Add(text).Answer + extractor.Complete();
        return result.Trim();
    }
}

public sealed record ThinkTagStreamResult(string Answer, IReadOnlyList<string> Reasonings);

public sealed class ThinkTagStreamExtractor
{
    private const string OpenTag = "<think>";
    private const string CloseTag = "</think>";
    private bool _insideThink;
    private string _pending = "";

    public ThinkTagStreamResult Add(string chunk)
    {
        var input = _pending + chunk;
        _pending = "";
        var answer = "";
        var reasonings = new List<string>();

        while (input.Length > 0)
        {
            if (_insideThink)
            {
                var closeIndex = IndexOfIgnoreCase(input, CloseTag);
                if (closeIndex < 0)
                {
                    var emitLength = Math.Max(0, input.Length - (CloseTag.Length - 1));
                    if (emitLength == 0)
                    {
                        _pending = input;
                        return new ThinkTagStreamResult(answer, reasonings);
                    }

                    reasonings.Add(input[..emitLength]);
                    _pending = input[emitLength..];
                    return new ThinkTagStreamResult(answer, reasonings);
                }

                reasonings.Add(input[..closeIndex]);
                input = input[(closeIndex + CloseTag.Length)..];
                _insideThink = false;
                continue;
            }

            var openIndex = IndexOfIgnoreCase(input, OpenTag);
            if (openIndex < 0)
            {
                var emitLength = Math.Max(0, input.Length - (OpenTag.Length - 1));
                if (emitLength == 0)
                {
                    _pending = input;
                    return new ThinkTagStreamResult(answer, reasonings);
                }

                answer += input[..emitLength];
                _pending = input[emitLength..];
                return new ThinkTagStreamResult(answer, reasonings);
            }

            answer += input[..openIndex];
            input = input[(openIndex + OpenTag.Length)..];
            _insideThink = true;
        }

        return new ThinkTagStreamResult(answer, reasonings);
    }

    public string Complete()
    {
        if (_pending.Length == 0)
        {
            return "";
        }

        var remainder = _insideThink ? "" : _pending;
        _pending = "";
        return remainder;
    }

    private static int IndexOfIgnoreCase(string input, string value) =>
        input.IndexOf(value, StringComparison.OrdinalIgnoreCase);
}
