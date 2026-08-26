using Microsoft.Extensions.DocumentExtraction;

namespace DocumentExtractionWorkbench.Core;

[Flags]
public enum DocumentExtractionProviderCapabilities
{
	None = 0,
	Text = 1 << 0,
	PageGeometry = 1 << 1,
	RegionGeometry = 1 << 2,
	PolygonGeometry = 1 << 3,
	Cancellation = 1 << 4,
	FixtureOnly = 1 << 5,
}

public enum DocumentExtractionProviderAvailability
{
	Unknown,
	Available,
	Unavailable,
}

public enum DocumentExtractionProviderReadiness
{
	Unknown,
	Ready,
	Busy,
	Error,
}

public sealed record DocumentExtractionProviderDescriptor(
	string ProviderId,
	string DisplayName,
	Uri? ProviderUri,
	string? DefaultModelId,
	DocumentExtractionProviderCapabilities Capabilities);

public sealed record DocumentExtractionProviderReadinessDescriptor(
	DocumentExtractionProviderAvailability Availability,
	DocumentExtractionProviderReadiness Readiness,
	string Details);

public sealed record DocumentExtractionProviderDiagnostics(
	DocumentExtractionProviderDescriptor Provider,
	DocumentExtractionProviderReadinessDescriptor Readiness)
{
	public static DocumentExtractionProviderDiagnostics FromClient(IDocumentExtractionClient client)
	{
		ArgumentNullException.ThrowIfNull(client);

		var provider = client.GetService(
			typeof(DocumentExtractionProviderDescriptor)) as DocumentExtractionProviderDescriptor;
		var readiness = client.GetService(
			typeof(DocumentExtractionProviderReadinessDescriptor)) as DocumentExtractionProviderReadinessDescriptor;
		var metadata = client.GetService(
			typeof(DocumentExtractionClientMetadata)) as DocumentExtractionClientMetadata;

		provider ??= new DocumentExtractionProviderDescriptor(
			ProviderId: "unreported",
			DisplayName: metadata?.ProviderName ?? "Provider identity not reported",
			ProviderUri: metadata?.ProviderUri,
			DefaultModelId: metadata?.DefaultModelId,
			Capabilities: DocumentExtractionProviderCapabilities.None);

		readiness ??= new DocumentExtractionProviderReadinessDescriptor(
			DocumentExtractionProviderAvailability.Unknown,
			DocumentExtractionProviderReadiness.Unknown,
			"The provider did not publish workbench readiness diagnostics.");

		return new DocumentExtractionProviderDiagnostics(provider, readiness);
	}
}
