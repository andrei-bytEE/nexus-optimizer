using System.Windows.Controls;
using NexusOptimizer.Models;
using NexusOptimizer.ViewModels;

namespace NexusOptimizer.Views;

public partial class OptimizationView : UserControl
{
    public OptimizationView()
    {
        InitializeComponent();
    }

    private void Apply_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { Tag: OptimizationItem item } &&
            DataContext is OptimizationViewModel vm)
        {
            vm.Toggle(item);
        }
    }
}