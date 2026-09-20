using System;
using YTSubConverter.Shared;

namespace TwitchChatCC.Subtitles;

public readonly record struct ChatMessage(Section Name, Section Message, TimeSpan Time);