using System.ComponentModel;
using DocumentExtractionWorkbench.Core;
using Xunit;

namespace Microsoft.Maui.Essentials.AI.UnitTests;

public sealed class DocumentExtractionWorkbenchViewModelTests
{
	[Fact]
	public async Task LoadFixtureAsync_UsesInjectedFixtureInputAndDispatchesReadyState()
	{
		var input = DocumentExtractionWorkbenchTestFixture.CreateFixtureInput();
		var inputService = new RecordingDocumentInputService { Fixture = input };
		var dispatcher = new RecordingWorkbenchDispatcher();
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		var viewModel = new DocumentExtractionWorkbenchViewModel(client, inputService, dispatcher);

		await viewModel.LoadFixtureAsync();

		Assert.Equal(1, inputService.FixtureLoadCount);
		Assert.Equal(1, dispatcher.InvocationCount);
		var source = Assert.IsType<DocumentInput>(viewModel.SourceDocument);
		Assert.Same(input, source);
		Assert.Equal(DocumentInputKind.Fixture, source.Kind);
		Assert.Equal(DocumentExtractionWorkbenchState.Ready, viewModel.State);
		Assert.Equal("Deterministic fixture loaded.", viewModel.StatusMessage);
		Assert.True(viewModel.CanExtract);
	}

	[Fact]
	public async Task ImportFileAsync_UsesInjectedFileInputAndKeepsImportedFileWorkflowVisible()
	{
		var file = new DocumentInput(
			"invoice.png",
			"image/png",
			"imported content"u8.ToArray(),
			DocumentInputKind.ImportedFile);
		var inputService = new RecordingDocumentInputService { File = file };
		var dispatcher = new RecordingWorkbenchDispatcher();
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		var viewModel = new DocumentExtractionWorkbenchViewModel(client, inputService, dispatcher);

		await viewModel.ImportFileAsync();

		Assert.Equal(1, inputService.FilePickCount);
		Assert.Equal(1, dispatcher.InvocationCount);
		var source = Assert.IsType<DocumentInput>(viewModel.SourceDocument);
		Assert.Same(file, source);
		Assert.Equal(DocumentInputKind.ImportedFile, source.Kind);
		Assert.Equal(DocumentExtractionWorkbenchState.Ready, viewModel.State);
		Assert.Equal(
			"Imported invoice.png. The deterministic provider will reject non-fixture extraction.",
			viewModel.StatusMessage);
		Assert.True(viewModel.CanExtract);
	}

	[Fact]
	public async Task ExtractAsync_TransitionsThroughRunningAndCompletedWithDispatchedState()
	{
		var inputService = new RecordingDocumentInputService
		{
			Fixture = DocumentExtractionWorkbenchTestFixture.CreateFixtureInput(),
		};
		var dispatcher = new RecordingWorkbenchDispatcher();
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		var viewModel = new DocumentExtractionWorkbenchViewModel(client, inputService, dispatcher);
		var stateTransitions = new List<DocumentExtractionWorkbenchState>();
		viewModel.PropertyChanged += (_, args) =>
		{
			if (args.PropertyName == nameof(DocumentExtractionWorkbenchViewModel.State))
			{
				stateTransitions.Add(viewModel.State);
			}
		};
		await viewModel.LoadFixtureAsync();
		var dispatchesBeforeExtract = dispatcher.InvocationCount;

		await viewModel.ExtractAsync();

		Assert.Equal(
			[DocumentExtractionWorkbenchState.Ready, DocumentExtractionWorkbenchState.Running, DocumentExtractionWorkbenchState.Completed],
			stateTransitions);
		Assert.Equal(dispatchesBeforeExtract + 3, dispatcher.InvocationCount);
		Assert.Equal(DocumentExtractionWorkbenchState.Completed, viewModel.State);
		Assert.False(viewModel.IsBusy);
		Assert.Equal(DocumentFixture.PageText, viewModel.ExtractedText);
		Assert.NotNull(viewModel.Geometry);
		Assert.Equal(6, viewModel.Geometry.Regions.Count);
		Assert.Equal("Extraction complete: 1 page(s).", viewModel.StatusMessage);
	}

	[Fact]
	public async Task ExtractAsync_CancellationUsesStartSignalWithoutTimingDelays()
	{
		var inputService = new RecordingDocumentInputService
		{
			Fixture = DocumentExtractionWorkbenchTestFixture.CreateFixtureInput(),
		};
		var dispatcher = new RecordingWorkbenchDispatcher();
		using var client = new CancellationControlledDocumentExtractionClient();
		var viewModel = new DocumentExtractionWorkbenchViewModel(client, inputService, dispatcher);
		await viewModel.LoadFixtureAsync();

		var extraction = viewModel.ExtractAsync();
		await client.Started.Task;
		await viewModel.CancelAsync();
		await extraction;

		Assert.Equal(DocumentExtractionWorkbenchState.Cancelled, viewModel.State);
		Assert.False(viewModel.IsBusy);
		Assert.Equal("Extraction cancelled.", viewModel.CancellationState);
		Assert.Equal("Extraction cancelled without publishing partial results.", viewModel.StatusMessage);
		Assert.False(viewModel.HasError);
		Assert.True(dispatcher.InvocationCount >= 5);
	}

	[Fact]
	public async Task ExtractAsync_WithImportedFilePublishesActionableProviderErrorState()
	{
		var importedFile = new DocumentInput(
			"invoice.png",
			"image/png",
			"unrecognized image"u8.ToArray(),
			DocumentInputKind.ImportedFile);
		var inputService = new RecordingDocumentInputService { File = importedFile };
		var dispatcher = new RecordingWorkbenchDispatcher();
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		var viewModel = new DocumentExtractionWorkbenchViewModel(client, inputService, dispatcher);
		await viewModel.ImportFileAsync();

		await viewModel.ExtractAsync();

		Assert.Equal(DocumentExtractionWorkbenchState.Failed, viewModel.State);
		Assert.True(viewModel.HasError);
		Assert.Contains(
			$"accepts only the embedded {DocumentFixture.MediaType} fixture",
			viewModel.ErrorMessage,
			StringComparison.Ordinal);
		Assert.Equal(
			"Extraction failed. Review the error and provider capabilities.",
			viewModel.StatusMessage);
		Assert.Null(viewModel.Geometry);
	}

	[Fact]
	public void Constructor_ExposesProviderDiagnosticsMetadataReadinessAndCapabilities()
	{
		var dispatcher = new RecordingWorkbenchDispatcher();
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		var viewModel = new DocumentExtractionWorkbenchViewModel(
			client,
			new RecordingDocumentInputService(),
			dispatcher);

		Assert.Equal(
			"DOC-H1 deterministic fixture provider (doc-h1-deterministic)",
			viewModel.ProviderIdentity);
		Assert.Equal("Available", viewModel.ProviderAvailability);
		Assert.Equal(
			"Ready: Ready for the embedded DOC-H1 fixture. Imported files require a different provider.",
			viewModel.ProviderReadiness);
		Assert.Equal(
			"Text, PageGeometry, RegionGeometry, PolygonGeometry, Cancellation, FixtureOnly",
			viewModel.ProviderCapabilities);
		Assert.True(viewModel.IsProviderReady);
		Assert.Equal(0, dispatcher.InvocationCount);
	}

	[Fact]
	public async Task ExtractAsync_WhenProviderIsUnavailable_BlocksExtractionWithReadinessDetails()
	{
		var inputService = new RecordingDocumentInputService
		{
			Fixture = DocumentExtractionWorkbenchTestFixture.CreateFixtureInput(),
		};
		var dispatcher = new RecordingWorkbenchDispatcher();
		using var client = new UnavailableDocumentExtractionClient();
		var viewModel = new DocumentExtractionWorkbenchViewModel(client, inputService, dispatcher);
		await viewModel.LoadFixtureAsync();

		Assert.False(viewModel.CanExtract);
		await viewModel.ExtractAsync();

		Assert.Equal(0, client.ExtractCallCount);
		Assert.Equal(DocumentExtractionWorkbenchState.Ready, viewModel.State);
		Assert.True(viewModel.HasError);
		Assert.Contains("selected provider is not ready", viewModel.ErrorMessage, StringComparison.Ordinal);
		Assert.Contains("Model assets are not installed.", viewModel.ErrorMessage, StringComparison.Ordinal);
		Assert.Equal("Unavailable", viewModel.ProviderAvailability);
		Assert.Equal("Error: Model assets are not installed.", viewModel.ProviderReadiness);
	}
}
