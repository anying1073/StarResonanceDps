namespace StarResonanceDpsAnalysis.WPF.Helpers;

public static class NameMasker
{
    public static string Mask(string name)
    {
        if (name.Length <= 1) return "*";
        if (name.Length == 2) return $"{name[0]}*";
        //if (name.Length <= 5) return $"{name[0]}**{name[^1]}";
        //return $"{name[..2]}**{name[^2..]}";
        return $"{name[0]}**{name[^1]}";
    }
}