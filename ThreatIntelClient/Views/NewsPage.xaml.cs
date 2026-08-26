using ThreatIntelClient.ViewModels;

namespace ThreatIntelClient.Views;

public partial class NewsPage : ContentPage
{
    public NewsPage(NewsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is NewsViewModel vm && vm.Articles.Count == 0)
        {
            _ = vm.LoadArticlesAsync(true);
        }
    }
}
