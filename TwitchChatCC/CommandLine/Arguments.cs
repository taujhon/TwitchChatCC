using System.CommandLine;

namespace TwitchChatCC.CommandLine;

public static class Arguments
{
public static readonly Argument<string[]> PathArgument = new("paths")
    {
        Description = "Input and output paths, in order: the first half are the chat replay JSON files to concatenate (in chronological order, e.g. as split across several VODs), and the last path is the output file",
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Argument<string> CsvArgument = new("csv-path")
    {
        Description = "Path to the CSV file with data to transform"
    };

    public static readonly Argument<string> SuffixArgument = new("suffix")
    {
        Description = "Suffix to be appended to all output file names, including the extension (e.g. \".ytt\", \"-cropped.json\")"
    };
}