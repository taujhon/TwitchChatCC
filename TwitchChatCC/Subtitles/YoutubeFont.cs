using System;
using System.Collections.Generic;

namespace TwitchChatCC.Subtitles;

public static class YoutubeFont
{
    private const string Roboto = "Roboto";

    private static readonly IReadOnlyDictionary<string, string> Fonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Roboto"] = "Roboto",

        ["Courier New"] = "Courier New",
        ["Courier"] = "Courier New",
        ["Nimbus Mono L"] = "Courier New",
        ["Cutive Mono"] = "Courier New",

        ["Times New Roman"] = "Times New Roman",
        ["Times"] = "Times New Roman",
        ["Georgia"] = "Times New Roman",
        ["Cambria"] = "Times New Roman",
        ["PT Serif Caption"] = "Times New Roman",

        ["Deja Vu Sans Mono"] = "Lucida Console",
        ["DejaVu Sans Mono"] = "Lucida Console",
        ["Lucida Console"] = "Lucida Console",
        ["Monaco"] = "Lucida Console",
        ["Consolas"] = "Lucida Console",
        ["PT Mono"] = "Lucida Console",

        ["Comic Sans Ms"] = "Comic Sans Ms",
        ["Impact"] = "Comic Sans Ms",
        ["Handlee"] = "Comic Sans Ms",

        ["Monotype Corsiva"] = "Monotype Corsiva",
        ["URW Chancery L"] = "Monotype Corsiva",
        ["Apple Chancery"] = "Monotype Corsiva",
        ["Dancing Script"] = "Monotype Corsiva",

        ["Carrois Gothic Sc"] = "Carrois Gothic Sc"
    };

    public static string Resolve(string? font, out bool recognized)
    {
        if (string.IsNullOrEmpty(font))
        {
            recognized = true;
            return Roboto;
        }

        if (Fonts.TryGetValue(font, out string? canonical))
        {
            recognized = true;
            return canonical;
        }

        recognized = false;
        PrintWarning($"Warning: \"{font}\" is not one of the fonts supported by YouTube; falling back to {Roboto} instead");
        return Roboto;
    }
}