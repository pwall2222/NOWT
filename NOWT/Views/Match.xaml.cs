using System.Windows;
using System.Windows.Controls;
using NOWT.ViewModels;

namespace NOWT.Views;

/// <summary>
///     Interaction logic for Match.xaml
/// </summary>
public partial class Match : UserControl
{
    public Match()
    {
        InitializeComponent();
        DataContextChanged += DataContextChangedHandler;
    }

    private void DataContextChangedHandler(object sender, DependencyPropertyChangedEventArgs e)
    {
        var viewModel = e.NewValue as MatchViewModel;

        if (viewModel == null)
            return;

        viewModel.GoHomeEvent += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (GoHome.Command.CanExecute(null))
                    GoHome.Command.Execute(null);
            });
        };
    }
}
