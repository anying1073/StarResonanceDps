using StarResonanceDpsAnalysis.Core.Analyze;

namespace StarResonanceDpsAnalysis.Core.Tests;

public class MethodExtensionTests
{
    [Fact]
    public void TryParseMessageMethod_ValidNames_ReturnsTrue()
    {
        foreach (var name in Enum.GetNames(typeof(MessageMethod)))
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
        Assert.Equal(default(MessageMethod), method);
    }
    [Fact]
    public void ToUInt32_And_FromUInt32_RoundTrip()
    {
        foreach (MessageMethod method in Enum.GetValues(typeof(MessageMethod)).Cast<MessageMethod>())
        {
            var uintValue = method.ToUInt32();
            var parsedMethod = MessageMethodExtensions.FromUInt32(uintValue);
            Assert.Equal(method, parsedMethod);
        }
    }
}