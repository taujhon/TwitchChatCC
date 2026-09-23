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
        Command.Add(PathArgument);
        Command.AddOptions<TransformOptions>();
        Command.SetAction(Execute);
    }

    private static void Execute(ParseResult parseResult)
    {
        string[] paths = parseResult.GetValue(PathArgument) ?? [];
        if (!TrySplitPaths(paths, out string outputPath, out string[] inputPaths))
            return;
        TransformOptions options = parseResult.ParseOptions<TransformOptions>();
        Response response = ResponseUtils.ValidateInputOutput(ref inputPaths, ref outputPath, options.Response);
        if (response == Response.No)
            return;
        foreach (string inputPath in inputPaths)
        {
            if (!IOUtils.ValidateFileExists(inputPath))
                return;
        }
        string[] inputs = new string[inputPaths.Length];
        for (int i = 0; i < inputPaths.Length; i++)
            inputs[i] = File.ReadAllText(inputPaths[i]);
        string output = Transform.DoTransform(inputs, options.Options);
        File.WriteAllText(outputPath, output);
    }

    public static bool TrySplitPaths(string[] paths, out string outputPath, out string[] inputPaths)
    {
        if (paths.Length >= 2)
        {
            outputPath = paths[^1];
            inputPaths = paths[..^1];
            return true;
        }
        PrintError("At least one input path and one output path are required", 2);
        outputPath = string.Empty;
        inputPaths = [];
        return false;
    }
}