using HeartRate.Mobile.ViewModels;

namespace HeartRate.Mobile.Views;

public partial class SessionsPage : ContentPage
{
    private readonly SessionsViewModel viewModel;

    public SessionsPage(SessionsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (viewModel.Sessions.Count == 0)
        {
            await viewModel.RefreshAsync();
        }
    }
}
