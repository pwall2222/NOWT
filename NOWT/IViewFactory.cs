using System.Windows;

namespace NOWT;

public interface IViewFactory
{
    FrameworkElement? ResolveView(object viewModel);
}