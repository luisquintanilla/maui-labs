using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DocumentExtraction;

namespace DocumentExtractionWorkbench.Core;

public enum DocumentExtractionWorkbenchState
{
	Idle,
	Ready,
	Running,
	Completed,
	Cancelled,
	Failed,
}

public sealed class DocumentExtractionWorkbenchViewModel : INotifyPropertyChanged
{
	private readonly IDocumentExtractionClient _client;
	private readonly IDocumentInputService _inputService;
	private readonly IWorkbenchDispatcher _dispatcher;
	private readonly object _operationGate = new();
	private CancellationTokenSource? _activeExtraction;
	private DocumentInput? _sourceDocument;
	private DocumentPageGeometry? _geometry;
	private DocumentExtractionWorkbenchState _state;
	private string _extractedText = string.Empty;
	private string _geometrySummary = "No page geometry has been extracted.";
	private string _errorMessage = string.Empty;
	private string _statusMessage = "Load the deterministic fixture or import an image.";
	private string _cancellationState = "No cancellation requested.";

	public DocumentExtractionWorkbenchViewModel(
		IDocumentExtractionClient client,
		IDocumentInputService inputService,
		IWorkbenchDispatcher dispatcher)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

		var diagnostics = DocumentExtractionProviderDiagnostics.FromClient(client);
		ProviderIdentity =
			$"{diagnostics.Provider.DisplayName} ({diagnostics.Provider.ProviderId})";
		ProviderAvailability = diagnostics.Readiness.Availability.ToString();
		ProviderReadiness =
			$"{diagnostics.Readiness.Readiness}: {diagnostics.Readiness.Details}";
		ProviderCapabilities = FormatCapabilities(diagnostics.Provider.Capabilities);
		IsProviderReady =
			diagnostics.Readiness.Availability == DocumentExtractionProviderAvailability.Available &&
			diagnostics.Readiness.Readiness == DocumentExtractionProviderReadiness.Ready;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public string ProviderIdentity { get; }

	public string ProviderAvailability { get; }

	public string ProviderReadiness { get; }

	public string ProviderCapabilities { get; }

	public bool IsProviderReady { get; }

	public DocumentInput? SourceDocument
	{
		get => _sourceDocument;
		private set => SetProperty(ref _sourceDocument, value);
	}

	public DocumentPageGeometry? Geometry
	{
		get => _geometry;
		private set => SetProperty(ref _geometry, value);
	}

	public DocumentExtractionWorkbenchState State
	{
		get => _state;
		private set
		{
			if (SetProperty(ref _state, value))
			{
				OnPropertyChanged(nameof(IsBusy));
				OnPropertyChanged(nameof(CanLoadInput));
				OnPropertyChanged(nameof(CanExtract));
				OnPropertyChanged(nameof(CanCancel));
			}
		}
	}

	public string ExtractedText
	{
		get => _extractedText;
		private set => SetProperty(ref _extractedText, value);
	}

	public string GeometrySummary
	{
		get => _geometrySummary;
		private set => SetProperty(ref _geometrySummary, value);
	}

	public string ErrorMessage
	{
		get => _errorMessage;
		private set
		{
			if (SetProperty(ref _errorMessage, value))
			{
				OnPropertyChanged(nameof(HasError));
			}
		}
	}

	public string StatusMessage
	{
		get => _statusMessage;
		private set => SetProperty(ref _statusMessage, value);
	}

	public string CancellationState
	{
		get => _cancellationState;
		private set => SetProperty(ref _cancellationState, value);
	}

	public bool IsBusy => State == DocumentExtractionWorkbenchState.Running;

	public bool CanLoadInput => !IsBusy;

	public bool CanExtract => SourceDocument is not null && IsProviderReady && !IsBusy;

	public bool CanCancel => IsBusy;

	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

	public async Task LoadFixtureAsync(CancellationToken cancellationToken = default)
	{
		if (!CanLoadInput)
		{
			await SetErrorAsync(
				"Cancel the active extraction before loading another document.")
				.ConfigureAwait(false);
			return;
		}

		try
		{
			var input = await _inputService.LoadFixtureAsync(cancellationToken)
				.ConfigureAwait(false);
			await _dispatcher.InvokeAsync(
				() => SetSourceDocument(input, "Deterministic fixture loaded."),
				cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			await _dispatcher.InvokeAsync(() =>
			{
				CancellationState = "Fixture loading was cancelled.";
				StatusMessage = "Fixture loading cancelled.";
			}).ConfigureAwait(false);
		}
		catch (DocumentInputException exception)
		{
			await SetErrorAsync(exception.Message).ConfigureAwait(false);
		}
		catch (IOException exception)
		{
			await SetErrorAsync(
				$"The fixture could not be read: {exception.Message}")
				.ConfigureAwait(false);
		}
	}

	public async Task ImportFileAsync(CancellationToken cancellationToken = default)
	{
		if (!CanLoadInput)
		{
			await SetErrorAsync(
				"Cancel the active extraction before importing another document.")
				.ConfigureAwait(false);
			return;
		}

		try
		{
			var input = await _inputService.PickFileAsync(cancellationToken)
				.ConfigureAwait(false);
			await _dispatcher.InvokeAsync(() =>
			{
				if (input is null)
				{
					StatusMessage = "File import cancelled; the current document was preserved.";
					return;
				}

				SetSourceDocument(
					input,
					$"Imported {input.DisplayName}. The deterministic provider will reject non-fixture extraction.");
			}, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			await _dispatcher.InvokeAsync(() =>
			{
				CancellationState = "File import was cancelled.";
				StatusMessage = "File import cancelled.";
			}).ConfigureAwait(false);
		}
		catch (DocumentInputException exception)
		{
			await SetErrorAsync(exception.Message).ConfigureAwait(false);
		}
		catch (IOException exception)
		{
			await SetErrorAsync(
				$"The selected file could not be read: {exception.Message}")
				.ConfigureAwait(false);
		}
		catch (UnauthorizedAccessException exception)
		{
			await SetErrorAsync(
				$"The selected file could not be opened. Check file permissions. {exception.Message}")
				.ConfigureAwait(false);
		}
	}

	public async Task ExtractAsync(CancellationToken cancellationToken = default)
	{
		if (SourceDocument is not { } input)
		{
			await SetErrorAsync(
				"Load the deterministic fixture or import an image before extracting.")
				.ConfigureAwait(false);
			return;
		}

		if (!IsProviderReady)
		{
			await SetErrorAsync(
				$"The selected provider is not ready. {ProviderReadiness}")
				.ConfigureAwait(false);
			return;
		}

		CancellationTokenSource? extractionCancellation = null;
		lock (_operationGate)
		{
			if (_activeExtraction is null)
			{
				extractionCancellation =
					CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				_activeExtraction = extractionCancellation;
			}
		}

		if (extractionCancellation is null)
		{
			await SetErrorAsync("An extraction is already running.")
				.ConfigureAwait(false);
			return;
		}

		await _dispatcher.InvokeAsync(() =>
		{
			State = DocumentExtractionWorkbenchState.Running;
			ErrorMessage = string.Empty;
			ExtractedText = string.Empty;
			Geometry = null;
			GeometrySummary = "Waiting for page geometry.";
			CancellationState = "No cancellation requested.";
			StatusMessage = $"Extracting {input.DisplayName}...";
		}).ConfigureAwait(false);

		try
		{
			using var stream = input.OpenReadStream();
			var result = await _client.ExtractAsync(
				stream,
				input.MediaType,
				cancellationToken: extractionCancellation.Token)
				.ConfigureAwait(false);
			extractionCancellation.Token.ThrowIfCancellationRequested();

			var page = result.Pages.FirstOrDefault();
			var geometry = page is null ? null : DocumentPageGeometry.FromPage(page);

			await _dispatcher.InvokeAsync(() =>
			{
				ExtractedText = result.Text;
				Geometry = geometry;
				GeometrySummary = FormatGeometrySummary(geometry);
				State = DocumentExtractionWorkbenchState.Completed;
				StatusMessage = $"Extraction complete: {result.Pages.Count} page(s).";
			}).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (extractionCancellation.IsCancellationRequested)
		{
			await _dispatcher.InvokeAsync(() =>
			{
				State = DocumentExtractionWorkbenchState.Cancelled;
				CancellationState = "Extraction cancelled.";
				StatusMessage = "Extraction cancelled without publishing partial results.";
			}).ConfigureAwait(false);
		}
		catch (DocumentWorkbenchException exception)
		{
			await SetExtractionErrorAsync(exception.Message).ConfigureAwait(false);
		}
		catch (InvalidDataException exception)
		{
			await SetExtractionErrorAsync(
				$"The provider could not parse the document: {exception.Message}")
				.ConfigureAwait(false);
		}
		catch (NotSupportedException exception)
		{
			await SetExtractionErrorAsync(
				$"The provider does not support this document: {exception.Message}")
				.ConfigureAwait(false);
		}
		catch (IOException exception)
		{
			await SetExtractionErrorAsync(
				$"The provider could not read the document stream: {exception.Message}")
				.ConfigureAwait(false);
		}
		finally
		{
			lock (_operationGate)
			{
				if (ReferenceEquals(_activeExtraction, extractionCancellation))
				{
					_activeExtraction = null;
				}
			}

			extractionCancellation.Dispose();
			await _dispatcher.InvokeAsync(() =>
			{
				OnPropertyChanged(nameof(IsBusy));
				OnPropertyChanged(nameof(CanLoadInput));
				OnPropertyChanged(nameof(CanExtract));
				OnPropertyChanged(nameof(CanCancel));
			}).ConfigureAwait(false);
		}
	}

	public async Task CancelAsync()
	{
		CancellationTokenSource? extractionCancellation;
		lock (_operationGate)
		{
			extractionCancellation = _activeExtraction;
		}

		if (extractionCancellation is null)
		{
			await _dispatcher.InvokeAsync(() =>
			{
				CancellationState = "No extraction is running.";
				StatusMessage = "There is no extraction to cancel.";
			}).ConfigureAwait(false);
			return;
		}

		await _dispatcher.InvokeAsync(() =>
		{
			CancellationState = "Cancellation requested.";
			StatusMessage = "Cancelling extraction...";
		}).ConfigureAwait(false);
		extractionCancellation.Cancel();
	}

	private void SetSourceDocument(DocumentInput input, string statusMessage)
	{
		SourceDocument = input;
		State = DocumentExtractionWorkbenchState.Ready;
		ExtractedText = string.Empty;
		Geometry = null;
		GeometrySummary = "No page geometry has been extracted.";
		ErrorMessage = string.Empty;
		CancellationState = "No cancellation requested.";
		StatusMessage = statusMessage;
		OnPropertyChanged(nameof(CanExtract));
	}

	private Task SetErrorAsync(string message) =>
		_dispatcher.InvokeAsync(() =>
		{
			ErrorMessage = message;
			StatusMessage = "Action required.";
		});

	private Task SetExtractionErrorAsync(string message) =>
		_dispatcher.InvokeAsync(() =>
		{
			State = DocumentExtractionWorkbenchState.Failed;
			ErrorMessage = message;
			StatusMessage = "Extraction failed. Review the error and provider capabilities.";
		});

	private static string FormatCapabilities(DocumentExtractionProviderCapabilities capabilities)
	{
		if (capabilities == DocumentExtractionProviderCapabilities.None)
		{
			return "None reported";
		}

		return string.Join(
			", ",
			Enum.GetValues<DocumentExtractionProviderCapabilities>()
				.Where(value =>
					value != DocumentExtractionProviderCapabilities.None &&
					capabilities.HasFlag(value)));
	}

	private static string FormatGeometrySummary(DocumentPageGeometry? geometry)
	{
		if (geometry is null)
		{
			return "The provider returned no pages.";
		}

		if (geometry.Width <= 0 ||
			geometry.Height <= 0 ||
			geometry.CoordinateUnit is null ||
			geometry.CoordinateOrigin is null)
		{
			return "The provider returned text but did not report complete page geometry metadata.";
		}

		var regionLines = geometry.Regions.Select(region =>
		{
			var bounds = region.Polygon.Count == 0
				? "empty polygon"
				: $"bounds ({region.Polygon.Min(point => point.X):0.###}, " +
				  $"{region.Polygon.Min(point => point.Y):0.###}) to " +
				  $"({region.Polygon.Max(point => point.X):0.###}, " +
				  $"{region.Polygon.Max(point => point.Y):0.###})";
			return $"{region.Label}: {region.Polygon.Count} vertices, {bounds}";
		});

		return string.Join(
			Environment.NewLine,
			[
				$"Page {geometry.PageNumber}: {geometry.Width:0.###} x {geometry.Height:0.###} " +
				$"{geometry.CoordinateUnit}; origin {geometry.CoordinateOrigin}",
				$"{geometry.Regions.Count} region(s)",
				.. regionLines,
			]);
	}

	private bool SetProperty<T>(
		ref T storage,
		T value,
		[CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(storage, value))
		{
			return false;
		}

		storage = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
