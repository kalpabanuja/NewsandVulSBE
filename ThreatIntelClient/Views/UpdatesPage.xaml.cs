using ThreatIntelClient.ViewModels;

namespace ThreatIntelClient.Views;

public partial class UpdatesPage : ContentPage
{
    public UpdatesPage(UpdatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is UpdatesViewModel vm)
        {
            await vm.ConnectAsync();
        }
    }
}
