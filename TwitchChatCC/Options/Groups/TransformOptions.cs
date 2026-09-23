using TwitchChatCC.ConsoleUtils;

namespace TwitchChatCC.Options.Groups;

public record TransformOptions : OptionGroup<TransformOptions>
    {
        public TransformCommonOptions Options = new();

        [CliOption(nameof(CliOptions.Response))]
        public Plicit<CliResponse> Response;
    }