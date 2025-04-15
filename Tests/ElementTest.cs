

using NUnit.Framework;

namespace JsonArchitect.Tests;

[TestFixture]
public class ElementTest
{
    [Test]
    public void EmptyJsonTest()
    {
        var reader = new JsonReader("");
        Assert.That(reader.ReadJson(), Is.Empty);
    }
}