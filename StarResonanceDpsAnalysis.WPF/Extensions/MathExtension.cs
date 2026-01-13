namespace StarResonanceDpsAnalysis.WPF.Extensions;

internal static class MathExtension
{
    /// <summary>
    /// Calculate rate (returns 0 if divider is 0)
    /// </summary>
    public static double Rate(double value, double divider)
    {
        return divider > 0 ? value / divider : 0;
    }

    /// <summary>
    /// Calculate percentage (returns 0 if divider is 0)
    /// </summary>
    /// <param name="value"></param>
    /// <param name="divider"></param>
    /// <returns></returns>
    public static double Percentage(double value, double divider)
    {
        return Rate(value, divider) * 100;
    }
}