using TwitchChatCC.Options;
using System;
using System.Collections.Generic;

namespace TwitchChatCC.Csv;

public readonly record struct HeaderGroup(Range HeaderRange, Dictionary<string, FieldData> DataMap);