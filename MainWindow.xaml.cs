using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NexusOptimizer.ViewModels;

namespace NexusOptimizer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += Vm_PropertyChanged;
        HighlightTab(_vm.CurrentPage);
    }

    private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentPage)) return;
        HighlightTab(_vm.CurrentPage);
        PlayPageTransition();
    }

    private void HighlightTab(object page)
    {
        SetActive(NavDashboard, page is DashboardViewModel);
        SetActive(NavPerformance, page is PerformanceViewModel);
        SetActive(NavOptimization, page is OptimizationViewModel);
        SetActive(NavProfiles, page is ProfilesViewModel);
        SetActive(NavStartup, page is StartupViewModel);
        SetActive(NavUninstall, page is UninstallAppsViewModel);
        SetActive(NavLicense, page is LicenseViewModel);
    }

    private static void SetActive(Button button, bool active)
    {
        if (active)
        {
            button.BorderBrush = Brushes.White;
            button.BorderThickness = new Thickness(2);
        }
        else
        {
            button.ClearValue(Button.BorderBrushProperty);
            button.ClearValue(Button.BorderThicknessProperty);
        }
    }

    private void PlayPageTransition()
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        ContentHost.BeginAnimation(OpacityProperty, fade);
        ((TranslateTransform)ContentHost.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slide);
    }
}