using CSVFile;

namespace TwitchChatCC.Csv;

public static class CsvUtils
{
    public static readonly CSVSettings CsvSettings = new()
    {
        FieldDelimiter = ','
    };
}