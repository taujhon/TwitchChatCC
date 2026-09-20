using System;

namespace TwitchChatCC;

public class InternalException(string? message = null) : Exception(message);