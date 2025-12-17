using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Controller;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        Closed += OnWindowClosed;
        KeyDown += OnKeyDown;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var keyChar = e.Key switch
        {
            Key.W => "W",
            Key.A => "A",
            Key.S => "S",
            Key.D => "D",
            _ => null
        };

        if (keyChar == null) return;

        _viewModel.SendMoveKeys(keyChar);
        e.Handled = true;
    }
}