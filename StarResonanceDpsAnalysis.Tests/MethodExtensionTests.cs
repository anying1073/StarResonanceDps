using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

namespace StarResonanceDpsAnalysis.Tests;

public class MethodExtensionTests
{
    [Fact]
    public void TryParseMessageMethod_ValidNames_ReturnsTrue()
    {
        foreach (var name in Enum.GetNames(typeof(WorldNtfMessageId)))
        {
            var result = MessageMethodExtensions.TryParseMessageMethod(name, out var method);
            Assert.True(result);
            Assert.Equal(name, method.ToString());
        }
    }
    [Fact]
    public void TryParseMessageMethod_InvalidName_ReturnsFalse()
    {
        var result = MessageMethodExtensions.TryParseMessageMethod("NonExistentMethod", out var method);
        Assert.False(result);
        Assert.Equal(default(WorldNtfMessageId), method);
    }
    [Fact]
    public void ToUInt32_And_FromUInt32_RoundTrip()
    {
        foreach (WorldNtfMessageId method in Enum.GetValues(typeof(WorldNtfMessageId)).Cast<WorldNtfMessageId>())
        {
            var uintValue = method.ToUInt32();
            var parsedMethod = MessageMethodExtensions.FromUInt32(uintValue);
            Assert.Equal(method, parsedMethod);
        }
    }
}