global using static TwitchChatCC.ConsoleUtils.ConsoleOutput;
using TwitchChatCC.CommandLine;

namespace TwitchChatCC;

public class Program
{
    public static int Main(string[] args) => RootCommand.Command.Parse(args).Invoke();
}
