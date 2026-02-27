namespace StarResonanceDpsAnalysis.WPF.Models;

public sealed record NetworkAdapterInfo(string Name, string Description)
{
    public bool Equals(NetworkAdapterInfo? other)
    {
        if (ReferenceEquals(other, null))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Name, other.Name, System.StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
}