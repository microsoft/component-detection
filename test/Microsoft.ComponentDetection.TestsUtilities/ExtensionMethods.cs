namespace Microsoft.ComponentDetection.TestsUtilities;
using System.IO;
using System.Text;

public static class ExtensionMethods
{
    public static Stream ToStream(this string input)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        stream.Seek(0, SeekOrigin.Begin);

        return stream;
    }
}
