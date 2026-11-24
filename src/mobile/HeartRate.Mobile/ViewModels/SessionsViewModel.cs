using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HeartRate.Mobile.Models;
using HeartRate.Mobile.Services;

namespace HeartRate.Mobile.ViewModels;

public sealed class SessionsViewModel : INotifyPropertyChanged
{
    private readonly IHeartRateApi api;
    private bool isBusy;
    private string? errorMessage;

    public SessionsViewModel(IHeartRateApi api)
    {
        this.api = api;
        RefreshCommand = new Command(
            async () => await RefreshAsync(),
            () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MeasurementSession> Sessions { get; } = [];

    public Command RefreshCommand { get; }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                RefreshCommand.ChangeCanExecute();
            }
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetField(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var sessions = await api.GetSessionsAsync();

            Sessions.Clear();
            foreach (var session in sessions)
            {
                Sessions.Add(session);
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Nie udało się pobrać sesji: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
