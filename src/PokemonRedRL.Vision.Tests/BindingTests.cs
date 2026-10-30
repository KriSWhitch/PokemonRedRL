using System.Text;
using Xunit;

namespace PokemonRedRL.Vision.Tests;

public class BindingTests
{
    private const string First = """
        {"Index":0,"Host":"127.0.0.1","Port":12345,"EmulatorProcessId":101,"WindowHandle":111}
        """;
    private const string Second = """
        {"Index":1,"Host":"127.0.0.1","Port":12346,"EmulatorProcessId":102,"WindowHandle":222}
        """;

    private static FrameBinding Parse(string slots, int index = 0, int port = 12345)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"RunId\":\"test\",\"Agents\":[" + slots + "]}"));
        return FrameBinding.Parse(stream, index, "127.0.0.1", port, new FrameRegion(0, 0, 160, 144));
    }

    [Fact]
    public void SlotsSelectOnlyTheirOwnWindows()
    {
        Assert.Equal(111, Parse(First + "," + Second).WindowHandle);
        Assert.Equal(222, Parse(First + "," + Second, 1, 12346).WindowHandle);
    }

    [Fact]
    public void DuplicateMissingAndMismatchedSlotsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Parse(First + "," + First));
        Assert.Throws<InvalidDataException>(() => Parse(Second));
        Assert.Throws<InvalidDataException>(() => Parse(First, port: 12346));
        Assert.Throws<InvalidDataException>(() => Parse(First.Replace("101", "null")));
        Assert.Throws<InvalidDataException>(() => Parse(First.Replace("111", "0")));
    }
}