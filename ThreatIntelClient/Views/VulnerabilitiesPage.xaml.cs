using ThreatIntelClient.ViewModels;
using ThreatIntelClient.Models;
using System.Linq;

namespace ThreatIntelClient.Views;

public partial class VulnerabilitiesPage : ContentPage
{
    public VulnerabilitiesPage(VulnerabilitiesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is VulnerabilitiesViewModel vm && vm.Cves.Count == 0)
        {
            _ = vm.LoadCvesAsync(true);
        }
    }

    private async void OnCveSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Cve selectedCve)
        {
            await DisplayAlert(selectedCve.Id, selectedCve.Description, "Close");
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}
