using DocumentExtractionWorkbench.Core;
using Microsoft.Extensions.DocumentExtraction;
using Xunit;

namespace Microsoft.Maui.Essentials.AI.UnitTests;

public sealed class DeterministicDocumentExtractionClientTests
{
	[Fact]
	public async Task ExtractAsync_ReturnsExactFixedTextAndHonestFixtureGeometry()
	{
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		await using var stream = new MemoryStream(DocumentExtractionWorkbenchTestFixture.ReadBytes());

		var result = await client.ExtractAsync(stream, DocumentFixture.MediaType);
		var page = Assert.Single(result.Pages);

		Assert.Equal(DocumentFixture.PageText, result.Text);
		Assert.Equal(1, result.Usage?.PagesProcessed);
		Assert.Equal(DocumentFixture.PageWidth, page.Dimensions?.Width);
		Assert.Equal(DocumentFixture.PageHeight, page.Dimensions?.Height);
		Assert.Equal((DocumentCoordinateUnit?)DocumentCoordinateUnit.Pixel, page.CoordinateUnit);
		Assert.Equal((DocumentCoordinateOrigin?)DocumentCoordinateOrigin.TopLeft, page.CoordinateOrigin);
		Assert.Equal(6, page.Elements.Count);

		Assert.Collection(page.Elements,
			element =>
			{
				var title = Assert.IsType<DocumentBlock>(element);
				Assert.Equal("DOCUMENT EXTRACTION\nWORKBENCH", title.Text);
				Assert.Equal(DocumentBlockKind.Title, title.Kind);
				Assert.Equal(
					[(42f, 42f), (558f, 42f), (558f, 126f), (42f, 126f)],
					title.BoundingRegion!.Polygon.Select(point => (point.X, point.Y)).ToArray());
			},
			element =>
			{
				var reference = Assert.IsType<DocumentBlock>(element);
				Assert.Equal("Reference: DOC-H1-001", reference.Text);
				Assert.Equal(DocumentBlockKind.Paragraph, reference.Kind);
				Assert.Equal(
					[(42f, 160f), (558f, 160f), (558f, 218f), (42f, 218f)],
					reference.BoundingRegion!.Polygon.Select(point => (point.X, point.Y)).ToArray());
			},
			element =>
			{
				var geometry = Assert.IsType<DocumentBlock>(element);
				Assert.Equal("Deterministic text and geometry\nPage coordinates: 600 x 800 pixels\nOrigin: top-left", geometry.Text);
				Assert.Equal(DocumentBlockKind.Paragraph, geometry.Kind);
				Assert.Equal(
					[(42f, 248f), (558f, 248f), (558f, 430f), (42f, 430f)],
					geometry.BoundingRegion!.Polygon.Select(point => (point.X, point.Y)).ToArray());
			},
			element =>
			{
				var note = Assert.IsType<DocumentBlock>(element);
				Assert.Equal("Fixed content. Fixed coordinates. No network or model.", note.Text);
				Assert.Equal(DocumentBlockKind.Paragraph, note.Kind);
				Assert.Equal(
					[(42f, 504f), (558f, 504f), (558f, 550f), (42f, 550f)],
					note.BoundingRegion!.Polygon.Select(point => (point.X, point.Y)).ToArray());
			},
			element =>
			{
				var ready = Assert.IsType<DocumentBlock>(element);
				Assert.Equal("READY", ready.Text);
				Assert.Equal(new DocumentBlockKind("status"), ready.Kind);
				Assert.Equal(
					[(399.171f, 656.009f), (548.349f, 640.331f), (554.829f, 701.991f), (405.651f, 717.669f)],
					ready.BoundingRegion!.Polygon.Select(point => (point.X, point.Y)).ToArray());
			},
			element =>
			{
				var footer = Assert.IsType<DocumentBlock>(element);
				Assert.Equal("DOC-H1 provider-neutral extraction fixture", footer.Text);
				Assert.Equal(DocumentBlockKind.Paragraph, footer.Kind);
				Assert.Equal(
					[(42f, 724f), (390f, 724f), (390f, 758f), (42f, 758f)],
					footer.BoundingRegion!.Polygon.Select(point => (point.X, point.Y)).ToArray());
			});
	}

	[Fact]
	public async Task ExtractAsync_RepeatedFixtureExtractionReturnsEquivalentResults()
	{
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		await using var firstStream = new MemoryStream(DocumentExtractionWorkbenchTestFixture.ReadBytes());
		await using var secondStream = new MemoryStream(DocumentExtractionWorkbenchTestFixture.ReadBytes());

		var first = await client.ExtractAsync(firstStream, DocumentFixture.MediaType);
		var second = await client.ExtractAsync(secondStream, $"{DocumentFixture.MediaType}; charset=utf-8");

		Assert.Equal(first.Text, second.Text);
		Assert.Equal(first.Pages.Single().Elements.Count, second.Pages.Single().Elements.Count);
		Assert.Equal(
			first.Pages.Single().Elements[4].BoundingRegion!.Polygon.Select(point => (point.X, point.Y)),
			second.Pages.Single().Elements[4].BoundingRegion!.Polygon.Select(point => (point.X, point.Y)));
	}

	[Fact]
	public async Task ExtractPagesAsync_StreamsTheSingleCompletedFixturePage()
	{
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		await using var stream = new MemoryStream(DocumentExtractionWorkbenchTestFixture.ReadBytes());
		var updates = new List<DocumentExtractionPageResult>();

		await foreach (var update in client.ExtractPagesAsync(stream, DocumentFixture.MediaType))
		{
			updates.Add(update);
		}

		var result = Assert.Single(updates);
		Assert.Equal(1, result.Page.PageNumber);
		Assert.Equal(DocumentFixture.PageText, result.Page.Text);
		Assert.Equal(1, result.PagesProcessed);
		Assert.Equal(1, result.TotalPages);
		Assert.Equal(1, result.Usage?.PagesProcessed);
	}

	[Fact]
	public async Task ExtractAsync_DoesNotDisposeTheCallerStream()
	{
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		using var stream = new TrackingMemoryStream(DocumentExtractionWorkbenchTestFixture.ReadBytes());

		var result = await client.ExtractAsync(stream, DocumentFixture.MediaType);

		Assert.False(stream.WasDisposed);
		Assert.True(stream.CanRead);
		Assert.Equal(DocumentFixture.PageText, result.Text);
	}

	[Fact]
	public async Task ExtractAsync_WithPreCancelledTokenThrowsBeforeProducingOutput()
	{
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		await using var stream = new MemoryStream(DocumentExtractionWorkbenchTestFixture.ReadBytes());
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			client.ExtractAsync(stream, DocumentFixture.MediaType, cancellationToken: cancellation.Token));
		Assert.Equal(0, stream.Position);
	}

	[Fact]
	public async Task ExtractAsync_WithNonFixtureContentReportsAnActionableProviderError()
	{
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);
		await using var stream = new MemoryStream("not the embedded fixture"u8.ToArray());

		var exception = await Assert.ThrowsAsync<UnsupportedDocumentInputException>(() =>
			client.ExtractAsync(stream, DocumentFixture.MediaType));

		Assert.Contains("selected document is not the DOC-H1 fixture", exception.Message, StringComparison.Ordinal);
		Assert.Contains("does not pretend its fixed geometry", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GetService_ReturnsMetadataProviderReadinessAndCapabilities()
	{
		using var client = new DeterministicDocumentExtractionClient(TimeSpan.Zero);

		var metadata = Assert.IsType<DocumentExtractionClientMetadata>(
			client.GetService(typeof(DocumentExtractionClientMetadata)));
		var provider = Assert.IsType<DocumentExtractionProviderDescriptor>(
			client.GetService(typeof(DocumentExtractionProviderDescriptor)));
		var readiness = Assert.IsType<DocumentExtractionProviderReadinessDescriptor>(
			client.GetService(typeof(DocumentExtractionProviderReadinessDescriptor)));

		Assert.Same(client, client.GetService(typeof(DeterministicDocumentExtractionClient)));
		Assert.Equal("DOC-H1 deterministic fixture provider", metadata.ProviderName);
		Assert.Equal(DocumentFixture.ModelId, metadata.DefaultModelId);
		Assert.Equal("doc-h1-deterministic", provider.ProviderId);
		Assert.True(provider.Capabilities.HasFlag(DocumentExtractionProviderCapabilities.Text));
		Assert.True(provider.Capabilities.HasFlag(DocumentExtractionProviderCapabilities.PolygonGeometry));
		Assert.True(provider.Capabilities.HasFlag(DocumentExtractionProviderCapabilities.Cancellation));
		Assert.Equal(DocumentExtractionProviderAvailability.Available, readiness.Availability);
		Assert.Equal(DocumentExtractionProviderReadiness.Ready, readiness.Readiness);
		Assert.Null(client.GetService(typeof(DocumentExtractionClientMetadata), "named"));
	}
}
