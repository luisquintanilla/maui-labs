using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DocumentExtraction;

namespace DocumentExtractionWorkbench.Core;

public sealed class DeterministicDocumentExtractionClient : IDocumentExtractionClient
{
	private static readonly DocumentExtractionProviderDescriptor ProviderDescriptor = new(
		ProviderId: "doc-h1-deterministic",
		DisplayName: "DOC-H1 deterministic fixture provider",
		ProviderUri: null,
		DefaultModelId: DocumentFixture.ModelId,
		Capabilities:
			DocumentExtractionProviderCapabilities.Text |
			DocumentExtractionProviderCapabilities.PageGeometry |
			DocumentExtractionProviderCapabilities.RegionGeometry |
			DocumentExtractionProviderCapabilities.PolygonGeometry |
			DocumentExtractionProviderCapabilities.Cancellation |
			DocumentExtractionProviderCapabilities.FixtureOnly);

	private static readonly DocumentExtractionProviderReadinessDescriptor ReadinessDescriptor = new(
		DocumentExtractionProviderAvailability.Available,
		DocumentExtractionProviderReadiness.Ready,
		"Ready for the embedded DOC-H1 fixture. Imported files require a different provider.");

	private static readonly DocumentExtractionClientMetadata Metadata = new(
		ProviderDescriptor.DisplayName,
		ProviderDescriptor.ProviderUri,
		ProviderDescriptor.DefaultModelId);

	private readonly TimeSpan _processingDelay;
	private int _disposed;

	public DeterministicDocumentExtractionClient(TimeSpan? processingDelay = null)
	{
		_processingDelay = processingDelay ?? TimeSpan.FromMilliseconds(600);
	}

	public async Task<DocumentExtractionResult> ExtractAsync(
		Stream document,
		string mediaType,
		DocumentExtractionOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		var page = await ExtractPageAsync(document, mediaType, options, cancellationToken)
			.ConfigureAwait(false);

		return new DocumentExtractionResult([page])
		{
			Usage = new DocumentExtractionUsage
			{
				PagesProcessed = 1,
			},
		};
	}

	public async IAsyncEnumerable<DocumentExtractionPageResult> ExtractPagesAsync(
		Stream document,
		string mediaType,
		DocumentExtractionOptions? options = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var page = await ExtractPageAsync(document, mediaType, options, cancellationToken)
			.ConfigureAwait(false);

		yield return new DocumentExtractionPageResult(page)
		{
			PagesProcessed = 1,
			TotalPages = 1,
			Usage = new DocumentExtractionUsage
			{
				PagesProcessed = 1,
			},
		};
	}

	public object? GetService(Type serviceType, object? serviceKey = null)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceKey is not null)
		{
			return null;
		}

		if (serviceType.IsInstanceOfType(this))
		{
			return this;
		}

		if (serviceType == typeof(DocumentExtractionProviderDescriptor))
		{
			return ProviderDescriptor;
		}

		if (serviceType == typeof(DocumentExtractionProviderReadinessDescriptor))
		{
			return ReadinessDescriptor;
		}

		if (serviceType == typeof(DocumentExtractionClientMetadata))
		{
			return Metadata;
		}

		return null;
	}

	public void Dispose()
	{
		Interlocked.Exchange(ref _disposed, 1);
	}

	private async Task<DocumentPage> ExtractPageAsync(
		Stream document,
		string mediaType,
		DocumentExtractionOptions? options,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(document);
		ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

		if (!document.CanRead)
		{
			throw new ArgumentException("The document stream must be readable.", nameof(document));
		}

		if (!IsFixtureMediaType(mediaType))
		{
			throw new UnsupportedDocumentInputException(
				$"The deterministic provider accepts only the embedded {DocumentFixture.MediaType} fixture. " +
				"Register a real IDocumentExtractionClient to extract imported files.");
		}

		if (options?.ModelId is { Length: > 0 } modelId &&
			!string.Equals(modelId, DocumentFixture.ModelId, StringComparison.Ordinal))
		{
			throw new UnsupportedDocumentInputException(
				$"Model '{modelId}' is not available. Use '{DocumentFixture.ModelId}' for the deterministic fixture.");
		}

		cancellationToken.ThrowIfCancellationRequested();

		if (_processingDelay > TimeSpan.Zero)
		{
			await Task.Delay(_processingDelay, cancellationToken).ConfigureAwait(false);
		}

		using var content = new MemoryStream();
		await document.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
		ValidateFixture(content.ToArray());
		cancellationToken.ThrowIfCancellationRequested();

		return CreateFixturePage();
	}

	private static bool IsFixtureMediaType(string mediaType)
	{
		var separator = mediaType.IndexOf(';');
		var normalized = separator >= 0 ? mediaType[..separator] : mediaType;
		return string.Equals(normalized.Trim(), DocumentFixture.MediaType, StringComparison.OrdinalIgnoreCase);
	}

	private static void ValidateFixture(byte[] content)
	{
		string svg;
		try
		{
			svg = new UTF8Encoding(
				encoderShouldEmitUTF8Identifier: false,
				throwOnInvalidBytes: true).GetString(content);
		}
		catch (DecoderFallbackException exception)
		{
			throw new UnsupportedDocumentInputException(
				"The deterministic provider expected the embedded UTF-8 SVG fixture.",
				exception);
		}

		var canonicalSvg = svg
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n');
		var hash = Convert.ToHexString(
			SHA256.HashData(Encoding.UTF8.GetBytes(canonicalSvg)));

		if (!string.Equals(
			hash,
			DocumentFixture.ExpectedCanonicalSha256,
			StringComparison.OrdinalIgnoreCase))
		{
			throw new UnsupportedDocumentInputException(
				"The selected document is not the DOC-H1 fixture. " +
				"The deterministic provider does not pretend its fixed geometry applies to imported content.");
		}
	}

	private static DocumentPage CreateFixturePage()
	{
		var title = new DocumentBlock("DOCUMENT EXTRACTION\nWORKBENCH")
		{
			Kind = DocumentBlockKind.Title,
			Confidence = 1,
			BoundingRegion = DocumentBoundingRegion.FromRectangle(1, 42, 42, 558, 126),
		};
		var reference = new DocumentBlock("Reference: DOC-H1-001")
		{
			Kind = DocumentBlockKind.Paragraph,
			Confidence = 1,
			BoundingRegion = DocumentBoundingRegion.FromRectangle(1, 42, 160, 558, 218),
		};
		var geometry = new DocumentBlock(
			"Deterministic text and geometry\nPage coordinates: 600 x 800 pixels\nOrigin: top-left")
		{
			Kind = DocumentBlockKind.Paragraph,
			Confidence = 1,
			BoundingRegion = DocumentBoundingRegion.FromRectangle(1, 42, 248, 558, 430),
		};
		var note = new DocumentBlock(
			"Fixed content. Fixed coordinates. No network or model.")
		{
			Kind = DocumentBlockKind.Paragraph,
			Confidence = 1,
			BoundingRegion = DocumentBoundingRegion.FromRectangle(1, 42, 504, 558, 550),
		};
		var ready = new DocumentBlock("READY")
		{
			Kind = new DocumentBlockKind("status"),
			Confidence = 1,
			BoundingRegion = new DocumentBoundingRegion(
				1,
				[
					new DocumentPoint(399.171f, 656.009f),
					new DocumentPoint(548.349f, 640.331f),
					new DocumentPoint(554.829f, 701.991f),
					new DocumentPoint(405.651f, 717.669f),
				]),
		};
		var footer = new DocumentBlock(
			"DOC-H1 provider-neutral extraction fixture")
		{
			Kind = DocumentBlockKind.Paragraph,
			Confidence = 1,
			BoundingRegion = DocumentBoundingRegion.FromRectangle(1, 42, 724, 390, 758),
		};

		return new DocumentPage(1, DocumentFixture.PageText)
		{
			Dimensions = new DocumentPageDimensions(
				DocumentFixture.PageWidth,
				DocumentFixture.PageHeight),
			CoordinateUnit = DocumentCoordinateUnit.Pixel,
			CoordinateOrigin = DocumentCoordinateOrigin.TopLeft,
			Elements = [title, reference, geometry, note, ready, footer],
		};
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(
			Volatile.Read(ref _disposed) != 0,
			this);
	}
}
