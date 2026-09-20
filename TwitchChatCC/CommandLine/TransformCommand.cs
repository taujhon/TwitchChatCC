using TwitchChatCC.ConsoleUtils;
using TwitchChatCC.Options;
using TwitchChatCC.Options.Groups;
using System.CommandLine;
using System.IO;
using static TwitchChatCC.CommandLine.Arguments;

namespace TwitchChatCC.CommandLine;

public static class TransformCommand
{
    public static readonly Command Command = new("transform", "Transform a JSON Twitch chat file and put the new contents in a new file");

    static TransformCommand()
    {
        Command.Add(InputArgument);
        Command.Add(OutputArgument);
        Command.AddOptions<TransformOptions>();
        Command.SetAction(Execute);
    }

    private static void Execute(ParseResult parseResult)
    {
        string inputPath = parseResult.GetValue(InputArgument)!;
        string outputPath = parseResult.GetValue(OutputArgument)!;
        TransformOptions options = parseResult.ParseOptions<TransformOptions>();
        Response response = ResponseUtils.ValidateInputOutput(ref inputPath, ref outputPath, options.Response);
        if (response == Response.No)
            return;
        string input = File.ReadAllText(inputPath);
        string output = Transform.DoTransform(input, options.Options);
        File.WriteAllText(outputPath, output);
    }
}