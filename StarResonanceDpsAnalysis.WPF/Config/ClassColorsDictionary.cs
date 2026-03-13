using System;
using System.Windows;
using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.Themes;

public class ClassColorsDictionary : ResourceDictionary
{
    public ClassColorsDictionary()
    {
        SetSourceBasedOnSelectedTemplate(ClassColorTemplate.Light);
    }

    public ClassColorTemplate Template
    {
        set => SetSourceBasedOnSelectedTemplate(value);
    }

    private void SetSourceBasedOnSelectedTemplate(ClassColorTemplate template)
    {
        var templateName = template switch
        {
            ClassColorTemplate.Dark => "Dark",
            _ => "Light"
        };

        Source = new Uri(
            $"pack://application:,,,/Styles/Classes/Classes.Colors.{templateName}.xaml",
            UriKind.Absolute);
    }
}
