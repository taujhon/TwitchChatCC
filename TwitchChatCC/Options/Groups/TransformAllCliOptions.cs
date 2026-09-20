using TwitchChatCC.ConsoleUtils;

namespace TwitchChatCC.Options.Groups;

public record TransformAllCliOptions : OptionGroup<TransformAllCliOptions>
{
    public TransformAllCommonOptions CommonOptions = new();

    [CliOption(nameof(CliOptions.Quiet))]
    public Plicit<bool> Quiet;

    [CliOption(nameof(CliOptions.MultiResponse))]
    public Plicit<CliMultiResponse> Response;

    [CliOption(nameof(CliOptions.CsvPath))]
    public Plicit<string> CsvPath;
}