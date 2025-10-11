using System.Reflection;

namespace StarResonanceDpsAnalysis.Core.Tests;

public static class TestUtils
{
    public static T GetFieldValue<T>(object instance, string name)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var property = instance.GetType().GetProperty(name, flags);
        if (property != null)
        {
            return (T)property.GetValue(instance)!;
        }

        var field = instance.GetType().GetField(name, flags);
        if (field != null)
        {
            return (T)field.GetValue(instance)!;
        }

        throw new ArgumentException($"Field or property '{name}' not found on type {instance.GetType().Name}");
    }
}
