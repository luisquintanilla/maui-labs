using System.Runtime.CompilerServices;
using DocumentExtractionWorkbench.Core;
using Microsoft.Extensions.DocumentExtraction;

namespace Microsoft.Maui.Essentials.AI.UnitTests;

internal static class DocumentExtractionWorkbenchTestFixture
{
	public static byte[] ReadBytes()
	{
		using var resource = typeof(DocumentExtractionWorkbenchTestFixture)
			.Assembly
			.GetManifestResourceStream(DocumentFixture.ResourceName)
			?? throw new InvalidOperationException(
				$"Embedded fixture '{DocumentFixture.ResourceName}' was not found.");

		using var content = new MemoryStream();
		resource.CopyTo(content);
		return content.ToArray();
	}

	public static DocumentInput CreateFixtureInput() =>
		DocumentFixture.CreateInput(ReadBytes());
}

internal sealed class TrackingMemoryStream(byte[] content) : MemoryStream(content, writable: false)
{
	public bool WasDisposed { get; private set; }

	protected override void Dispose(bool disposing)
	{
		WasDisposed = true;
		base.Dispose(disposing);
	}
}

internal sealed class RecordingDocumentInputService : IDocumentInputService
{
	public DocumentInput? Fixture { get; init; }

	public DocumentInput? File { get; init; }

	public int FixtureLoadCount { get; private set; }

	public int FilePickCount { get; private set; }

	public Task<DocumentInput> LoadFixtureAsync(CancellationToken cancellationToken = default)
	{
		FixtureLoadCount++;
		return Task.FromResult(Fixture ?? throw new InvalidOperationException("No fixture was configured."));
	}

	public Task<DocumentInput?> PickFileAsync(CancellationToken cancellationToken = default)
	{
		FilePickCount++;
		return Task.FromResult(File);
	}
}

internal sealed class RecordingWorkbenchDispatcher : IWorkbenchDispatcher
{
	public int InvocationCount { get; private set; }

	public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
	{
		InvocationCount++;
		action();
		return Task.CompletedTask;
	}
}

internal sealed class CancellationControlledDocumentExtractionClient : IDocumentExtractionClient
{
	private static readonly DocumentExtractionProviderDescriptor Provider = new(
		"cancellation-controlled",
		"Cancellation controlled provider",
		null,
		"controlled-v1",
		DocumentExtractionProviderCapabilities.Cancellation);

	private static readonly DocumentExtractionProviderReadinessDescriptor Readiness = new(
		DocumentExtractionProviderAvailability.Available,
		DocumentExtractionProviderReadiness.Ready,
		"Ready for cancellation control.");

	public TaskCompletionSource<bool> Started { get; } =
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	public async Task<DocumentExtractionResult> ExtractAsync(
		Stream document,
		string mediaType,
		DocumentExtractionOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		Started.TrySetResult(true);
		await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
		throw new InvalidOperationException("Cancellation was expected before extraction completed.");
	}

	public async IAsyncEnumerable<DocumentExtractionPageResult> ExtractPagesAsync(
		Stream document,
		string mediaType,
		DocumentExtractionOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await Task.CompletedTask;
		yield break;
	}

	public object? GetService(Type serviceType, object? serviceKey = null) =>
		serviceKey is not null
			? null
			: serviceType == typeof(DocumentExtractionProviderDescriptor)
				? Provider
				: serviceType == typeof(DocumentExtractionProviderReadinessDescriptor)
					? Readiness
					: serviceType == typeof(DocumentExtractionClientMetadata)
						? new DocumentExtractionClientMetadata(
							Provider.DisplayName,
							Provider.ProviderUri,
							Provider.DefaultModelId)
						: serviceType.IsInstanceOfType(this)
							? this
							: null;

	public void Dispose()
	{
	}
}

internal sealed class UnavailableDocumentExtractionClient : IDocumentExtractionClient
{
	private static readonly DocumentExtractionProviderDescriptor Provider = new(
		"unavailable",
		"Unavailable provider",
		null,
		"unavailable-v1",
		DocumentExtractionProviderCapabilities.Text);

	private static readonly DocumentExtractionProviderReadinessDescriptor Readiness = new(
		DocumentExtractionProviderAvailability.Unavailable,
		DocumentExtractionProviderReadiness.Error,
		"Model assets are not installed.");

	public int ExtractCallCount { get; private set; }

	public Task<DocumentExtractionResult> ExtractAsync(
		Stream document,
		string mediaType,
		DocumentExtractionOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		ExtractCallCount++;
		throw new InvalidOperationException("An unavailable provider must not be called.");
	}

	public async IAsyncEnumerable<DocumentExtractionPageResult> ExtractPagesAsync(
		Stream document,
		string mediaType,
		DocumentExtractionOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await Task.CompletedTask;
		yield break;
	}

	public object? GetService(Type serviceType, object? serviceKey = null) =>
		serviceKey is not null
			? null
			: serviceType == typeof(DocumentExtractionProviderDescriptor)
				? Provider
				: serviceType == typeof(DocumentExtractionProviderReadinessDescriptor)
					? Readiness
					: serviceType == typeof(DocumentExtractionClientMetadata)
						? new DocumentExtractionClientMetadata(
							Provider.DisplayName,
							Provider.ProviderUri,
							Provider.DefaultModelId)
						: serviceType.IsInstanceOfType(this)
							? this
							: null;

	public void Dispose()
	{
	}
}
