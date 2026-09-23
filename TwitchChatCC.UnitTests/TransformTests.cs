using TwitchChatCC.Badges;
using TwitchChatCC.CommandLine;
using TwitchChatCC.ConsoleUtils;
using TwitchChatCC.Options.Groups;
using TwitchChatCC.Json;
using TwitchChatCC.Subtitles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YTSubConverter.Shared;
using System.Linq;
using System.IO;

namespace TwitchChatCC.UnitTests;

public class TransformTests
{
    private static long[] AllStartsEnds => [0, 1, 10, 100, 1000, -1, -10, -100, -1000, long.MinValue, long.MaxValue];
    private static long[] AllNegativeEnds => [-1, -10, -100, -1000, long.MinValue];
    private static Format[] AllFormats => [Format.Json, Format.JsonIndented, Format.Ytt, Format.Plaintext];
    private static SubtitleOptions DefaultSubtitleOptions => new()
    {
        Position = new(AnchorPoint.TopLeft, true),
        MaxMessages = new(4, true),
        MaxCharsPerLine = new(40, true),
        SubFontSize = new(18, true),
        SubBackgroundEnable = new(false, true),
        SubOutlineDisable = new(false, true),
        SubColorSeed = new(0, true),
        WindowOpacity = new(0, true),
        TextColor = new("white", true),
        SectionOptions = new()
        {
            SubScale = new(0.0, true),
            Shadow = new(Shadow.Glow, true),
            SubBackgroundOpacity = new(0, true),
            ShadowColor = new("black", true),
            SubBackgroundColor = new("black", true),
            SubFont = new("Roboto", true)
        }
    };

    private const string ContentOffsetSecondsTemplate = "\"content_offset_seconds\":0";
    private const string CommenterTemplate = "\"commenter\":{\"display_name\":\"JohnSmith\"}";
    private const string MessageTemplate = "\"message\":{\"body\":\"Hello, World!\"}";

    private static TransformCommonOptions GetOptions(Format format, bool badges = false, string badgeConfig = "")
        => new()
        {
            Start = new(0, true),
            End = new(-1, true),
            Delay = new(0, true),
            Format = new(format, true),
            Badges = new(badges, true),
            BadgeConfig = new(badgeConfig, true),
            SubtitleOptions = DefaultSubtitleOptions
        };

    private static string WriteBadgeConfig(string json)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        return path;
    }

    private static string SegmentJson(params (long offset, string name)[] comments)
        => $"{{\"comments\":[{string.Join(",", comments.Select(c => $"{{\"content_offset_seconds\":{c.offset},\"commenter\":{{\"display_name\":\"{c.name}\"}},\"message\":{{\"body\":\"msg{c.offset}\",\"user_color\":\"#FF0000\"}}}}"))}]}}";

    private static string SegmentJson(long videoLength, params (long offset, string name)[] comments)
        => $"{{\"video\":{{\"length\":{videoLength}}},\"comments\":[{string.Join(",", comments.Select(c => $"{{\"content_offset_seconds\":{c.offset},\"commenter\":{{\"display_name\":\"{c.name}\"}},\"message\":{{\"body\":\"msg{c.offset}\",\"user_color\":\"#FF0000\"}}}}"))}]}}";

    private static long[] GetCommentOffsets(JToken json)
        => json.D("comments").As<JArray>().Select(c => c.D("content_offset_seconds").As<long>()).ToArray();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("\n")]
    [InlineData("\t")]
    [InlineData("  \t\n\r  ")]
    public void DoTransform_EmptyOrWhitespaceJsonString_ThrowsJsonContentExceptionEmpty(string inputString)
    {
        long[] starts = AllStartsEnds;
        long[] ends = AllStartsEnds;
        Format[] formats = AllFormats;

        JsonContentException.Empty expectedException = new();

        foreach (long start in starts)
        {
            foreach (long end in ends)
            {
                foreach (Format format in formats)
                {
                    TransformCommonOptions options = new()
                    {
                        Start = new(start, true),
                        End = new(end, true),
                        Format = new(format, true),
                        SubtitleOptions = DefaultSubtitleOptions
                    };
                    void DoTransform() => Transform.DoTransform(inputString, options);

                    JsonContentException.Empty exception = Assert.Throws<JsonContentException.Empty>(DoTransform);
                    Assert.Equal(expectedException.Message, exception.Message);

                    #pragma warning disable CS0162
                    continue; Transform.DoTransform(inputString, options);
                    #pragma warning restore CS0162
                }
            }
        }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("{(}")]
    [InlineData("()")]
    [InlineData("(")]
    public void DoTransform_InvalidJsonString_ThrowsJsonException(string inputString)
    {
        long[] starts = AllStartsEnds;
        long[] ends = AllStartsEnds;
        Format[] formats = AllFormats;

        foreach (long start in starts)
        {
            foreach (long end in ends)
            {
                foreach (Format format in formats)
                {
                    TransformCommonOptions options = new()
                    {
                        Start = new(start, true),
                        End = new(end, true),
                        Format = new(format, true),
                        SubtitleOptions = DefaultSubtitleOptions
                    };
                    void DoTransform() => Transform.DoTransform(inputString, options);

                    Assert.ThrowsAny<JsonException>(DoTransform);

                    #pragma warning disable CS0162
                    continue; Transform.DoTransform(inputString, options);
                    #pragma warning restore CS0162
                }
            }
        }
    }

    [Theory]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}}]}}")]
    public void ApplyOffset_Start0EndNegativeDelay0_DoesNothing(string inputString)
    {
        (JToken[] allComments, JToken json) = Transform.GetSortedOriginalCommentsAndJson(inputString);
        long start = 0;
        long[] ends = AllNegativeEnds;
        long delay = 0;

        foreach (long end in ends)
        {
            TransformCommonOptions options = new()
            {
                Start = new(start, true),
                End = new(end, true),
                Delay = new(delay, true),
                SubtitleOptions = DefaultSubtitleOptions
            };
            Transform.ApplyOffset(allComments, json, options);
            string output = JsonConvert.SerializeObject(json);

            Assert.Equal(inputString, output);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"comment\":[]}")]
    [InlineData("{\"commentss\":[]}")]
    public void GetSortedOriginalCommentsAndJson_NoComments_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        JsonContentException.PropertyNotFound expectedException = new(string.Empty, "comments");

        void GetSortedOriginalCommentsAndJson() => Transform.GetSortedOriginalCommentsAndJson(inputString);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(GetSortedOriginalCommentsAndJson);
        Assert.Equal(expectedException.Message, exception.Message);

        #pragma warning disable CS0162
        return; Transform.GetSortedOriginalCommentsAndJson(inputString);
        #pragma warning restore CS0162
    }

    [Theory]
    [InlineData($"{{\"comments\":[{{{CommenterTemplate},{MessageTemplate}}}]}}")]
    //[InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}},{{{CommenterTemplate},{MessageTemplate}}}]}}")]
    //[InlineData($"{{\"comments\":[{{{CommenterTemplate},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}}]}}")]
    public void GetSortedOriginalCommentsAndJson_NoContentOffsetSeconds_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        string expectedPathRegex = @"comments\[\d*\]";
        string expectedPropertyName = "content_offset_seconds";

        void GetSortedOriginalCommentsAndJson() => Transform.GetSortedOriginalCommentsAndJson(inputString);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(GetSortedOriginalCommentsAndJson);
        Assert.Matches(expectedPathRegex, exception.Path);
        Assert.Equal(expectedPropertyName, exception.PropertyName);

        #pragma warning disable CS0162
        return; Transform.GetSortedOriginalCommentsAndJson(inputString);
        #pragma warning restore CS0162
    }

    [Theory]
    [InlineData("{\"comments\":[]}", 0, -1, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[]}", 0, long.MinValue, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[]}", long.MaxValue, 0, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 0, -1, 0, "{\"comments\":[{\"content_offset_seconds\":5}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 0, -1, 0, "{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 0, 4, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 0, 5, 0, "{\"comments\":[{\"content_offset_seconds\":5}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 3, -1, 0, "{\"comments\":[{\"content_offset_seconds\":2}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 3, 5, 0, "{\"comments\":[{\"content_offset_seconds\":2}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 6, -1, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 6, 7, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 5, 5, 0, "{\"comments\":[{\"content_offset_seconds\":0}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 5, 4, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 4, 4, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 6, 6, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 5, -1, 0, "{\"comments\":[{\"content_offset_seconds\":0}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 3, 15, 0, "{\"comments\":[{\"content_offset_seconds\":2}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 3, 17, 0, "{\"comments\":[{\"content_offset_seconds\":2},{\"content_offset_seconds\":14}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 3, -1, 0, "{\"comments\":[{\"content_offset_seconds\":2},{\"content_offset_seconds\":14}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 5, -1, 0, "{\"comments\":[{\"content_offset_seconds\":0},{\"content_offset_seconds\":12}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 5, 16, 0, "{\"comments\":[{\"content_offset_seconds\":0}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 5, 17, 0, "{\"comments\":[{\"content_offset_seconds\":0},{\"content_offset_seconds\":12}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, 17, 0, "{\"comments\":[{\"content_offset_seconds\":11}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, -1, 0, "{\"comments\":[{\"content_offset_seconds\":11}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, 16, 0, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, 18, 0, "{\"comments\":[{\"content_offset_seconds\":11}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 17, 17, 0, "{\"comments\":[{\"content_offset_seconds\":0}]}")]
    [InlineData("{\"comments\":[]}", 0, -1, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[]}", 0, long.MinValue, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[]}", long.MaxValue, 0, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 0, -1, 13, "{\"comments\":[{\"content_offset_seconds\":18}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 0, -1, 13, "{\"comments\":[{\"content_offset_seconds\":18},{\"content_offset_seconds\":30}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 0, 4, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 0, 5, 13, "{\"comments\":[{\"content_offset_seconds\":18}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 3, -1, 13, "{\"comments\":[{\"content_offset_seconds\":15}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 3, 5, 13, "{\"comments\":[{\"content_offset_seconds\":15}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 6, -1, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 6, 7, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 5, 5, 13, "{\"comments\":[{\"content_offset_seconds\":13}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 5, 4, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 4, 4, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 6, 6, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5}]}", 5, -1, 13, "{\"comments\":[{\"content_offset_seconds\":13}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 3, 15, 13, "{\"comments\":[{\"content_offset_seconds\":15}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 3, 17, 13, "{\"comments\":[{\"content_offset_seconds\":15},{\"content_offset_seconds\":27}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 3, -1, 13, "{\"comments\":[{\"content_offset_seconds\":15},{\"content_offset_seconds\":27}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 5, -1, 13, "{\"comments\":[{\"content_offset_seconds\":13},{\"content_offset_seconds\":25}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 5, 16, 13, "{\"comments\":[{\"content_offset_seconds\":13}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 5, 17, 13, "{\"comments\":[{\"content_offset_seconds\":13},{\"content_offset_seconds\":25}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, 17, 13, "{\"comments\":[{\"content_offset_seconds\":24}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, -1, 13, "{\"comments\":[{\"content_offset_seconds\":24}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, 16, 13, "{\"comments\":[]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 6, 18, 13, "{\"comments\":[{\"content_offset_seconds\":24}]}")]
    [InlineData("{\"comments\":[{\"content_offset_seconds\":5},{\"content_offset_seconds\":17}]}", 17, 17, 13, "{\"comments\":[{\"content_offset_seconds\":13}]}")]
    public void ApplyOffset_ValidInput(string inputString, long start, long end, long delay, string expectedOutput)
    {
        (JToken[] allComments, JToken json) = Transform.GetSortedOriginalCommentsAndJson(inputString);

        TransformCommonOptions options = new()
        {
            Start = new(start, true),
            End = new(end, true),
            Delay = new(delay, true),
            SubtitleOptions = DefaultSubtitleOptions
        };
        Transform.ApplyOffset(allComments, json, options);
        string output = JsonConvert.SerializeObject(json);

        Assert.Equal(expectedOutput, output);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"comment\":[]}")]
    [InlineData("{\"commentss\":[]}")]
    public void SerializeToPlaintext_NoComments_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        JsonContentException.PropertyNotFound expectedException = new(string.Empty, "comments");

        void SerializeToPlaintext() => Transform.SerializeToPlaintext(json);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(SerializeToPlaintext);
        Assert.Equal(expectedException.Message, exception.Message);

        #pragma warning disable CS0162
        return; Transform.SerializeToPlaintext(json);
        #pragma warning restore CS0162
    }

    [Theory]
    [InlineData($"{{\"comments\":[{{{CommenterTemplate},{MessageTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}},{{{CommenterTemplate},{MessageTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{CommenterTemplate},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}}]}}")]
    public void SerializeToPlaintext_NoContentOffsetSeconds_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string expectedPathRegex = @"comments\[\d*\]";
        string expectedPropertyName = "content_offset_seconds";

        void SerializeToPlaintext() => Transform.SerializeToPlaintext(json);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(SerializeToPlaintext);
        Assert.Matches(expectedPathRegex, exception.Path);
        Assert.Equal(expectedPropertyName, exception.PropertyName);

        #pragma warning disable CS0162
        return; Transform.SerializeToPlaintext(json);
        #pragma warning restore CS0162
    }

    [Theory]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{MessageTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},{MessageTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}}]}}")]
    public void SerializeToPlaintext_NoCommenter_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string expectedPathRegex = @"comments\[\d*\]";
        string expectedPropertyName = "commenter";

        void SerializeToPlaintext() => Transform.SerializeToPlaintext(json);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(SerializeToPlaintext);
        Assert.Matches(expectedPathRegex, exception.Path);
        Assert.Equal(expectedPropertyName, exception.PropertyName);

        #pragma warning disable CS0162
        return; Transform.SerializeToPlaintext(json);
        #pragma warning restore CS0162
    }

    [Theory]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},\"commenter\":{{}},{MessageTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},\"commenter\":{{}},{MessageTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},\"commenter\":{{}},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}}]}}")]
    public void SerializeToPlaintext_NoDisplayName_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string expectedPathRegex = @"comments\[\d*\]\.commenter";
        string expectedPropertyName = "display_name";

        void SerializeToPlaintext() => Transform.SerializeToPlaintext(json);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(SerializeToPlaintext);
        Assert.Matches(expectedPathRegex, exception.Path);
        Assert.Equal(expectedPropertyName, exception.PropertyName);

        #pragma warning disable CS0162
        return; Transform.SerializeToPlaintext(json);
        #pragma warning restore CS0162
    }

    [Theory]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}}]}}")]
    public void SerializeToPlaintext_NoMessage_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string expectedPathRegex = @"comments\[\d*\]";
        string expectedPropertyName = "message";

        void SerializeToPlaintext() => Transform.SerializeToPlaintext(json);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(SerializeToPlaintext);
        Assert.Matches(expectedPathRegex, exception.Path);
        Assert.Equal(expectedPropertyName, exception.PropertyName);

        #pragma warning disable CS0162
        return; Transform.SerializeToPlaintext(json);
        #pragma warning restore CS0162
    }

    [Theory]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},\"message\":{{}}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate},\"message\":{{}}}}]}}")]
    [InlineData($"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},\"message\":{{}}}},{{{ContentOffsetSecondsTemplate},{CommenterTemplate},{MessageTemplate}}}]}}")]
    public void SerializeToPlaintext_NoBody_ThrowsJsonContentExceptionPropertyNotFound(string inputString)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string expectedPathRegex = @"comments\[\d*\]\.message";
        string expectedPropertyName = "body";

        void SerializeToPlaintext() => Transform.SerializeToPlaintext(json);

        JsonContentException.PropertyNotFound exception = Assert.Throws<JsonContentException.PropertyNotFound>(SerializeToPlaintext);
        Assert.Matches(expectedPathRegex, exception.Path);
        Assert.Equal(expectedPropertyName, exception.PropertyName);

        #pragma warning disable CS0162
        return; Transform.SerializeToPlaintext(json);
        #pragma warning restore CS0162
    }

    // for the plaintext we avoid the raw string literal """ since the interpretation of an end of line will depend on the environment
    // e.g. on Windows in Visual Studio an end of line is interprted as \r\n, but in other systems it may be \n
    // for the actual SerializeToPlaintext method, we prefer just \n and let the OS convert it if appropriate
    [Theory]
    [InlineData(
        """
        {
            "comments": [
                {
                    "content_offset_seconds": 0,
                    "commenter": {
                        "display_name": "JohnSmith"
                    },
                    "message": {
                        "body": "Hello, World!"
                    }
                },
                {
                    "content_offset_seconds": 17,
                    "commenter": {
                        "display_name": "JaneSmith"
                    },
                    "message": {
                        "body": "Hello as well!"
                    }
                },
                {
                    "content_offset_seconds": 7533,
                    "commenter": {
                        "display_name": "JohnSmith"
                    },
                    "message": {
                        "body": "It's been over an hour."
                    }
                },
                {
                    "content_offset_seconds": 93252,
                    "commenter": {
                        "display_name": "JohnSmith"
                    },
                    "message": {
                        "body": "Now it's been over a day."
                    }
                },
                {
                    "content_offset_seconds": 52715391,
                    "commenter": {
                        "display_name": "JohnSmith"
                    },
                    "message": {
                        "body": "Now it's been over a year."
                    }
                }
            ]
        }
        """,
        "00:00:00 JohnSmith: Hello, World!\n" +
        "00:00:17 JaneSmith: Hello as well!\n" +
        "02:05:33 JohnSmith: It's been over an hour.\n" +
        "1.01:54:12 JohnSmith: Now it's been over a day.\n" +
        "610.03:09:51 JohnSmith: Now it's been over a year.\n")]
    public void SerializeToPlaintext_ValidInput(string inputString, string expectedOutput)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string output = Transform.SerializeToPlaintext(json);

        Assert.Equal(expectedOutput, output);
    }

    [Theory]
    [InlineData(
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[]}}]}",
        "00:00:00 JohnSmith: Hello, World!\n")]
    [InlineData(
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"}]}}]}",
        "00:00:00 🛡️ JohnSmith: Hello, World!\n")]
    [InlineData(
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"},{\"_id\":\"subscriber\",\"version\":\"2\"}]}}]}",
        "00:00:00 🛡️⭐ JohnSmith: Hello, World!\n")]
    [InlineData(
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"totallyunknownbadge\",\"version\":\"1\"}]}}]}",
        "00:00:00 JohnSmith: Hello, World!\n")]
    public void SerializeToPlaintext_BadgesEnabled_NoConfig_DefaultMapIsUsed(string inputString, string expectedOutput)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string output = Transform.SerializeToPlaintext(json, BadgeMap.Default);

        Assert.Equal(expectedOutput, output);
    }

    [Theory]
    [InlineData("{\"moderator\":\"M\",\"vip\":\"V\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"},{\"_id\":\"vip\",\"version\":\"1\"}]}}]}",
        "00:00:00 MV JohnSmith: Hello, World!\n")]
    [InlineData("{\"custombadge\":\"🎉\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"custombadge\",\"version\":\"1\"}]}}]}",
        "00:00:00 🎉 JohnSmith: Hello, World!\n")]
    [InlineData("{\"moderator\":\"\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"}]}}]}",
        "00:00:00 JohnSmith: Hello, World!\n")]
    public void SerializeToPlaintext_BadgesEnabled_ConfigMergesOverDefault(string configJson, string inputString, string expectedOutput)
    {
        string configPath = WriteBadgeConfig(configJson);
        JToken json = JsonUtils.Deserialize(inputString);
        try
        {
            BadgeMap badgeMap = BadgeMap.Load(configPath);

            string output = Transform.SerializeToPlaintext(json, badgeMap);

            Assert.Equal(expectedOutput, output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Theory]
    [InlineData("{\"moderator:0\":\"🟠\",\"moderator\":\"🛡️\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"}]}}]}",
        "00:00:00 🟠 JohnSmith: Hello, World!\n")]
    [InlineData("{\"moderator:0\":\"🟠\",\"moderator\":\"🛡️\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"3\"}]}}]}",
        "00:00:00 🛡️ JohnSmith: Hello, World!\n")]
    public void SerializeToPlaintext_BadgesEnabled_VersionSpecificEntryTakesPrecedence(string configJson, string inputString, string expectedOutput)
    {
        string configPath = WriteBadgeConfig(configJson);
        JToken json = JsonUtils.Deserialize(inputString);
        try
        {
            BadgeMap badgeMap = BadgeMap.Load(configPath);

            string output = Transform.SerializeToPlaintext(json, badgeMap);

            Assert.Equal(expectedOutput, output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Theory]
    [InlineData("{\"*\":\"🎫\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"someunknownbadge\",\"version\":\"1\"},{\"_id\":\"anotherone\",\"version\":\"0\"}]}}]}",
        "00:00:00 🎫🎫 JohnSmith: Hello, World!\n")]
    [InlineData("{\"*\":\"🎫\",\"moderator\":\"🛡️\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"},{\"_id\":\"someunknownbadge\",\"version\":\"1\"}]}}]}",
        "00:00:00 🛡️🎫 JohnSmith: Hello, World!\n")]
    public void SerializeToPlaintext_BadgesEnabled_WildcardFallsBackForUnmappedBadges(string configJson, string inputString, string expectedOutput)
    {
        string configPath = WriteBadgeConfig(configJson);
        JToken json = JsonUtils.Deserialize(inputString);
        try
        {
            BadgeMap badgeMap = BadgeMap.Load(configPath);

            string output = Transform.SerializeToPlaintext(json, badgeMap);

            Assert.Equal(expectedOutput, output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Theory]
    [InlineData(
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"}]}}]}",
        "00:00:00 JohnSmith: Hello, World!\n")]
    public void SerializeToPlaintext_BadgesDisabled_SoNothingIsRendered(string inputString, string expectedOutput)
    {
        JToken json = JsonUtils.Deserialize(inputString);

        string output = Transform.Serialize(json, GetOptions(Format.Plaintext));

        Assert.Equal(expectedOutput, output);
    }

    [Theory]
    [InlineData(
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"}]}}]}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"}]}}]}")]
    public void Serialize_FormatJson_BadgesEnabledAlthoughJsonIsUntouched(string inputString, string expectedOutput)
    {
        string output = Transform.Serialize(JsonUtils.Deserialize(inputString), GetOptions(Format.Json, badges: true));

        Assert.Equal(expectedOutput, output);
    }

    [Theory]
    [InlineData(
        "{\"moderator\":\"M\",\"subscriber\":\"S\",\"princessdonutbrown\":\"🍩\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"founder\",\"version\":\"0\"},{\"_id\":\"princessdonutbrown\",\"version\":\"1\"}]}}]}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"founder\",\"version\":\"0\"},{\"_id\":\"princessdonutbrown\",\"version\":\"1\"}]}}]}")]
    public void Serialize_FormatJson_BadgesEnabledWithConfig_JsonIsStillUntouched(string configJson, string inputString, string expectedOutput)
    {
        string configPath = WriteBadgeConfig(configJson);
        try
        {
            string output = Transform.Serialize(JsonUtils.Deserialize(inputString), GetOptions(Format.Json, badges: true, badgeConfig: configPath));

            Assert.Equal(expectedOutput, output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Theory]
    [InlineData(
        "{\"moderator\":\"M\",\"princessdonutbrown\":\"🍩\"}",
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"founder\",\"version\":\"0\"},{\"_id\":\"princessdonutbrown\",\"version\":\"1\"}]}}]}",
        "00:00:00 💜🍩 JohnSmith: Hello, World!\n")]
    public void Serialize_FormatPlaintext_BadgesEnabledWithConfig_ResolvesMixedDefaultsAndCustom(string configJson, string inputString, string expectedOutput)
    {
        string configPath = WriteBadgeConfig(configJson);
        try
        {
            string output = Transform.Serialize(JsonUtils.Deserialize(inputString), GetOptions(Format.Plaintext, badges: true, badgeConfig: configPath));

            Assert.Equal(expectedOutput, output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Theory]
    [InlineData("{\"moderator\":42}")]
    public void BadgeMap_Load_NonStringValue_ThrowsBadgeConfigExceptionInvalidValue(string configJson)
    {
        string configPath = WriteBadgeConfig(configJson);
        try
        {
            void Load() => BadgeMap.Load(configPath);

            Assert.Throws<BadgeConfigException.InvalidValue>(Load);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public void BadgeMap_Load_MissingFile_ThrowsBadgeConfigExceptionFileNotFound()
    {
        string configPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());

        void Load() => BadgeMap.Load(configPath);

        Assert.Throws<BadgeConfigException.FileNotFound>(Load);
    }

    [Fact]
    public void DefaultConfigJson_ProducesValidJsonContainingAllDefaultEntries()
    {
        JObject config = JsonUtils.Deserialize(BadgeMap.DefaultConfigJson);

        Assert.Equal("📺", config.D("broadcaster").As<string>());
        Assert.Equal("🛡️", config.D("moderator").As<string>());
        Assert.Equal("⭐", config.D("subscriber").As<string>());
        Assert.Equal("💜", config.D("founder").As<string>());
        Assert.Equal("🤖", config.D("confirmed-bot").As<string>());
    }

    [Theory]
    [InlineData(
        "{\"comments\":[{\"content_offset_seconds\":0,\"commenter\":{\"display_name\":\"JohnSmith\"},\"message\":{\"body\":\"Hello, World!\",\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"0\"},{\"_id\":\"vip\",\"version\":\"1\"}]}}]}",
        "00:00:00 🛡️💎 JohnSmith: Hello, World!\n")]
    public void WriteDefaultConfig_CreatesFileThatCanBeLoadedAndUsed(string inputString, string expectedOutput)
    {
        string configPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + ".json");
        try
        {
            BadgeMap.WriteDefaultConfig(configPath);
            BadgeMap badgeMap = BadgeMap.Load(configPath);

            string output = Transform.SerializeToPlaintext(JsonUtils.Deserialize(inputString), badgeMap);

            Assert.Equal(expectedOutput, output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Theory]
    [InlineData(null, "Roboto")]
    [InlineData("roboto", "Roboto")]
    [InlineData("Courier New", "Courier New")]
    [InlineData("courier", "Courier New")]
    [InlineData("Times New Roman", "Times New Roman")]
    [InlineData("georgia", "Times New Roman")]
    [InlineData("lucida console", "Lucida Console")]
    [InlineData("consolas", "Lucida Console")]
    [InlineData("comic sans ms", "Comic Sans Ms")]
    [InlineData("impact", "Comic Sans Ms")]
    [InlineData("monotype corsiva", "Monotype Corsiva")]
    [InlineData("dancing script", "Monotype Corsiva")]
    [InlineData("carrois gothic sc", "Carrois Gothic Sc")]
    public void YoutubeFont_Resolve_KnownFont_ReturnsCanonicalName(string? font, string expected)
    {
        string result = YoutubeFont.Resolve(font, out bool recognized);

        Assert.Equal(expected, result);
        Assert.True(recognized);
    }

    [Fact]
    public void YoutubeFont_Resolve_UnknownFont_ReturnsRobotoUnrecognized()
    {
        string result = YoutubeFont.Resolve("Papyrus", out bool recognized);

        Assert.Equal("Roboto", result);
        Assert.False(recognized);
    }

    private const string SingleCommentJson = $"{{\"comments\":[{{{ContentOffsetSecondsTemplate},{CommenterTemplate},\"message\":{{\"body\":\"Hello, World!\",\"user_color\":\"#FF0000\"}}}}]}}";

    [Theory]
    [InlineData("comic sans ms", "5")]
    [InlineData("consolas", "3")]
    [InlineData("impact", "5")]
    public void Serialize_FormatYtt_SubFont_EmitsFontStyleAttribute(string font, string expectedStyleId)
    {
        TransformCommonOptions options = GetOptions(Format.Ytt);
        options.SubtitleOptions.SectionOptions.SubFont = new(font, true);

        string output = Transform.DoTransform(SingleCommentJson, options);

        Assert.Contains($"fs=\"{expectedStyleId}\"", output);
    }

    [Fact]
    public void Serialize_FormatYtt_DefaultSubFont_RendersRobotoWithoutFontStyleAttribute()
    {
        string output = Transform.DoTransform(SingleCommentJson, GetOptions(Format.Ytt));

        Assert.DoesNotContain("fs=\"0\"", output);
    }

    [Theory]
    [InlineData("courier new", "Courier New")]
    [InlineData("monotype corsiva", "Monotype Corsiva")]
    public void Serialize_FormatAss_SubFont_EmitsFontNameOverride(string font, string expectedName)
    {
        TransformCommonOptions options = GetOptions(Format.Ass);
        options.SubtitleOptions.SectionOptions.SubFont = new(font, true);

        string output = Transform.DoTransform(SingleCommentJson, options);

        Assert.Contains($"\\fn{expectedName}", output);
    }

    [Fact]
    public void Serialize_FormatAss_DefaultSubScale_DoesNotEmitZeroFontSizeTag()
    {
        string output = Transform.DoTransform(SingleCommentJson, GetOptions(Format.Ass));

        Assert.DoesNotContain("\\fs0", output);
    }

    [Fact]
    public void GetSortedOriginalCommentsAndJson_MultipleInputs_ShiftsSegmentsByCumulativeVideoLength()
    {
        string file1 = SegmentJson(videoLength: 100, (5, "A"), (20, "B"));
        string file2 = SegmentJson(videoLength: 60, (3, "C"), (50, "D"));
        string file3 = SegmentJson((10, "E"));

        (JToken[] allComments, JToken json) = Transform.GetSortedOriginalCommentsAndJson([file1, file2, file3]);

        Assert.Equal(new long[] { 5, 20, 103, 150, 170 }, GetCommentOffsets(json));
        Assert.Equal(new string[] { "A", "B", "C", "D", "E" }, allComments.Select(c => c.D("commenter").D("display_name").As<string>()).ToArray());
        Assert.Equal(100, json.D("video").D("length").As<long>());
    }

    [Fact]
    public void GetSortedOriginalCommentsAndJson_MissingVideoLength_FallsBackToLastCommentOffset()
    {
        string file1 = SegmentJson((5, "A"), (8, "B"));
        string file2 = SegmentJson(videoLength: 30, (4, "C"));

        (JToken[] allComments, JToken json) = Transform.GetSortedOriginalCommentsAndJson([file1, file2]);

        Assert.Equal(new long[] { 5, 8, 12 }, GetCommentOffsets(json));
        Assert.Equal(allComments.Length, json.D("comments").As<JArray>().Count);
    }

    [Fact]
    public void GetSortedOriginalCommentsAndJson_SingleInputArray_BehavesLikeSingleString()
    {
        string input = SegmentJson(videoLength: 100, (5, "A"), (20, "B"));
        (JToken[] expectedAllComments, JToken expectedJson) = Transform.GetSortedOriginalCommentsAndJson(input);

        (JToken[] allComments, JToken json) = Transform.GetSortedOriginalCommentsAndJson([input]);

        Assert.Equal(JsonConvert.SerializeObject(expectedJson), JsonConvert.SerializeObject(json));
        Assert.Equal(expectedAllComments.Length, allComments.Length);
    }

    [Fact]
    public void DoTransform_MultipleInputs_MergesIntoCombinedJson()
    {
        string file1 = SegmentJson(videoLength: 100, (5, "A"), (20, "B"));
        string file2 = SegmentJson(videoLength: 60, (3, "C"));

        string output = Transform.DoTransform([file1, file2], GetOptions(Format.Json));

        Assert.Equal(new long[] { 5, 20, 103 }, GetCommentOffsets(JsonUtils.Deserialize(output)));
    }

    [Fact]
    public void DoTransform_MultipleInputs_StartEndDelayApplyToCombinedTimeline()
    {
        string file1 = SegmentJson(videoLength: 100, (5, "A"));
        string file2 = SegmentJson(videoLength: 60, (30, "B"));

        TransformCommonOptions options = GetOptions(Format.Json);
        options.Start = new(6, true);
        options.End = new(131, true);
        options.Delay = new(10, true);

        string output = Transform.DoTransform([file1, file2], options);

        Assert.Equal(new long[] { 134 }, GetCommentOffsets(JsonUtils.Deserialize(output)));
    }

    [Fact]
    public void DoTransform_MultipleInputs_YttContainsAllMergedMessages()
    {
        string file1 = SegmentJson(videoLength: 100, (5, "A"));
        string file2 = SegmentJson(videoLength: 60, (30, "B"));

        string output = Transform.DoTransform([file1, file2], GetOptions(Format.Ytt));

        Assert.Contains("msg5", output);
        Assert.Contains("msg30", output);
    }

    [Fact]
    public void TrySplitPaths_TwoOrMorePaths_LastIsOutputRestAreInputs()
    {
        bool result = TransformCommand.TrySplitPaths(["a.json", "b.json", "out.ytt"], out string output, out string[] inputs);

        Assert.True(result);
        Assert.Equal("out.ytt", output);
        Assert.Equal(new[] { "a.json", "b.json" }, inputs);
    }

    [Fact]
    public void TrySplitPaths_FewerThanTwoPaths_Fails()
    {
        bool result = TransformCommand.TrySplitPaths(["only-one.json"], out string output, out string[] inputs);

        Assert.False(result);
        Assert.Equal(string.Empty, output);
        Assert.Empty(inputs);
    }

    [Fact]
    public void ValidateInputOutput_OutputDiffersFromAllInputs_ReturnsYes()
    {
        string[] inputPaths = ["a.json", "b.json"];
        string outputPath = "out.ytt";

        Response response = ResponseUtils.ValidateInputOutput(ref inputPaths, ref outputPath, CliResponse.No);

        Assert.Equal(Response.Yes, response);
        Assert.Equal(new[] { "a.json", "b.json" }, inputPaths);
        Assert.Equal("out.ytt", outputPath);
    }

    [Theory]
    [InlineData(CliResponse.Yes, Response.Yes)]
    [InlineData(CliResponse.No, Response.No)]
    public void ValidateInputOutput_OutputMatchesAnyInput_ReturnsCliResponse(CliResponse cliResponse, Response expected)
    {
        string[] inputPaths = ["a.json", "b.json"];
        string outputPath = "b.json";

        Response response = ResponseUtils.ValidateInputOutput(ref inputPaths, ref outputPath, cliResponse);

        Assert.Equal(expected, response);
    }
}