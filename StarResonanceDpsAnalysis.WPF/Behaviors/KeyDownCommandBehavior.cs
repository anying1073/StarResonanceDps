using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;

namespace StarResonanceDpsAnalysis.WPF.Behaviors;

/// <summary>
/// Behavior to forward PreviewKeyDown events to a command
/// </summary>
public class KeyDownCommandBehavior : Behavior<TextBox>
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(KeyDownCommandBehavior));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        }
        base.OnDetaching();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Command?.CanExecute(e) == true)
        {
            Command.Execute(e);
        }
    }
}