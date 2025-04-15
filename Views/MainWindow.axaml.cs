using Avalonia.Controls;
using JsonArchitect.ViewModels;

namespace JsonArchitect.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(this);
        
    }
}