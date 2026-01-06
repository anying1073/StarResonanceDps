namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// 格式字段选项
/// </summary>
public class FormatFieldOption
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Placeholder { get; set; }
    public string Example { get; set; }

    public FormatFieldOption(string id, string displayName, string placeholder, string example)
    {
        Id = id;
        DisplayName = displayName;
        Placeholder = placeholder;
        Example = example;
    }
}
