using System.Windows;

namespace NexusOptimizer.Views;

public partial class PrivacyWindow : Window
{
    public PrivacyWindow()
    {
        InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        Services.PrivacyAcceptance.Save();
        Close();
    }
}