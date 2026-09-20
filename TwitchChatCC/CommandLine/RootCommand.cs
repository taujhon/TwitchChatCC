using TwitchChatCC.Badges;
using TwitchChatCC.Options.Groups;
using System;
using System.CommandLine;

namespace TwitchChatCC.CommandLine;

public static class RootCommand
{
    static RootCommand()
    {
        Command.Add(CliOptions.BadgeConfigDefault.Option);
        Command.Add(TransformCommand.Command);
        Command.Add(TransformManyCommand.Command);
        Command.Add(TransformAllCommand.Command);
        Command.SetAction(Execute);
    }

    public static readonly System.CommandLine.RootCommand Command = new("Tools for handling Twitch chat JSON files");

    private static int Execute(ParseResult parseResult)
    {
        if (parseResult.GetResult(CliOptions.BadgeConfigDefault.Option) is { Implicit: false })
        {
            string? path = parseResult.GetValue(CliOptions.BadgeConfigDefault.Option);
            if (string.IsNullOrEmpty(path))
            {
                PrintError("Error: a non-empty output path is required for --badge-config-default");
                return 1;
            }
            BadgeMap.WriteDefaultConfig(path);
            return 0;
        }
        Console.Error.WriteLine("Required command was not provided.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Run 'TwitchChatCC --help' to see available commands.");
        return 1;
    }
}