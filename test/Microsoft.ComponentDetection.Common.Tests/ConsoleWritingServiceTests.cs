#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ConsoleWritingServiceTests
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext inputTestContext)
    {
    }

    [TestMethod]
    public void Write_Writes()
    {
        var service = new ConsoleWritingService();
        var guid = Guid.NewGuid().ToString();
        var writer = new StringWriter();
        Console.SetOut(writer);
        service.Write(guid);
        var obj = new object();
        writer.ToString()
            .Should().Contain(guid);
    }
}
