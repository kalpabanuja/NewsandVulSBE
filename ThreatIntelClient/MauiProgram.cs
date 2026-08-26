using Microsoft.Extensions.Logging;

namespace ThreatIntelClient;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<Services.LocalDatabaseService>();
		builder.Services.AddSingleton<Services.SignalRClientService>();
		builder.Services.AddSingleton<Services.SettingsService>();

		builder.Services.AddTransient<ViewModels.VulnerabilitiesViewModel>();
		builder.Services.AddTransient<Views.VulnerabilitiesPage>();

		builder.Services.AddTransient<ViewModels.NewsViewModel>();
		builder.Services.AddTransient<Views.NewsPage>();

		builder.Services.AddTransient<ViewModels.UpdatesViewModel>();
		builder.Services.AddTransient<Views.UpdatesPage>();

		builder.Services.AddTransient<ViewModels.SettingsViewModel>();
		builder.Services.AddTransient<Views.SettingsPage>();

		return builder.Build();
	}
}
