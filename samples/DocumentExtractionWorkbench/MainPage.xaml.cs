using System.ComponentModel;
using DocumentExtractionWorkbench.Core;
using Microsoft.Extensions.DocumentExtraction;

namespace DocumentExtractionWorkbench;

public partial class MainPage : ContentPage
{
	private readonly DocumentExtractionWorkbenchViewModel _viewModel;
	private readonly DocumentOverlayDrawable _overlayDrawable = new();

	public MainPage(
		IDocumentExtractionClient client,
		IDocumentInputService inputService)
	{
		InitializeComponent();
		_viewModel = new DocumentExtractionWorkbenchViewModel(
			client,
			inputService,
			new MauiWorkbenchDispatcher(Dispatcher));
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		BindingContext = _viewModel;
		GeometryOverlay.Drawable = _overlayDrawable;
	}

	private async void OnLoadFixtureClicked(object? sender, EventArgs e) =>
		await _viewModel.LoadFixtureAsync();

	private async void OnImportFileClicked(object? sender, EventArgs e) =>
		await _viewModel.ImportFileAsync();

	private async void OnExtractClicked(object? sender, EventArgs e) =>
		await _viewModel.ExtractAsync();

	private async void OnCancelClicked(object? sender, EventArgs e) =>
		await _viewModel.CancelAsync();

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DocumentExtractionWorkbenchViewModel.SourceDocument))
		{
			UpdateSourceImage(_viewModel.SourceDocument);
		}

		if (e.PropertyName == nameof(DocumentExtractionWorkbenchViewModel.Geometry))
		{
			_overlayDrawable.Geometry = _viewModel.Geometry;
			GeometryOverlay.Invalidate();
		}
	}

	private void UpdateSourceImage(DocumentInput? input)
	{
		SourceImage.Source = input switch
		{
			null => null,
			{ Kind: DocumentInputKind.Fixture } => ImageSource.FromFile(DocumentFixture.ImageAssetName),
			_ => ImageSource.FromStream(input.OpenReadStream),
		};
	}
}
