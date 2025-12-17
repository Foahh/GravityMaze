using Avalonia;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace Controller;

public partial class LabeledControl : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<LabeledControl, string?>(nameof(Label));

    public static readonly StyledProperty<object?> ChildProperty =
        AvaloniaProperty.Register<LabeledControl, object?>(nameof(Child));

    public LabeledControl()
    {
        InitializeComponent();
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    [Content]
    public object? Child
    {
        get => GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }
}