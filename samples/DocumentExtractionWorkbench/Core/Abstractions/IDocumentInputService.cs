namespace DocumentExtractionWorkbench.Core;

public interface IDocumentInputService
{
	Task<DocumentInput> LoadFixtureAsync(CancellationToken cancellationToken = default);

	Task<DocumentInput?> PickFileAsync(CancellationToken cancellationToken = default);
}
