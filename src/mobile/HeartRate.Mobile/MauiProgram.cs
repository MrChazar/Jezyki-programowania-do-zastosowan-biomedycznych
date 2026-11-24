using HeartRate.Mobile.Services;
using HeartRate.Mobile.ViewModels;
using HeartRate.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace HeartRate.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri("http://10.0.2.2:5000/"),
            Timeout = TimeSpan.FromSeconds(15)
        });
        builder.Services.AddSingleton<IHeartRateApi, HeartRateApi>();
        builder.Services.AddTransient<SessionsViewModel>();
        builder.Services.AddTransient<SessionsPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
