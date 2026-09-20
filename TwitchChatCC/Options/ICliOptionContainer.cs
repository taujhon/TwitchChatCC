using System.CommandLine;

namespace TwitchChatCC.Options;

public interface ICliOptionContainer
{
    Option Option { get; }
    AliasesContainer AliasesContainer { get; }
}