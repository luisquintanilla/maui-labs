using DocumentExtractionWorkbench.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DocumentExtraction;
using Microsoft.Maui.Storage;

namespace DocumentExtractionWorkbench;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		builder.Services.AddSingleton<IFilePicker>(_ => FilePicker.Default);
		builder.Services.AddSingleton<IDocumentInputService>(services =>
			new MauiDocumentInputService(
				services.GetRequiredService<IFilePicker>(),
				typeof(MauiProgram).Assembly));
		builder.Services.AddSingleton<IDocumentExtractionClient, DeterministicDocumentExtractionClient>();
		builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}
