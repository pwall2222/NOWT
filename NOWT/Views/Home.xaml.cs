using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FontAwesome6.Fonts;
using NOWT.ViewModels;

namespace NOWT.Views;

public partial class Home : UserControl
{
    public static ImageAwesome ValorantStatus;
    public static ImageAwesome AccountStatus;
    public static ImageAwesome MatchStatus;


    public Home()
    {
        InitializeComponent();
        DataContextChanged += DataContextChangedHandler;

        ValorantStatus = ValorantStatusView;
        AccountStatus = AccountStatusView;
        MatchStatus = MatchStatusView;
    }


    private void DataContextChangedHandler(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is HomeViewModel viewModel)
            viewModel.GoMatchEvent += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (GoMatch.Command.CanExecute(null)) GoMatch.Command.Execute(null);
                });
            };
    }
}