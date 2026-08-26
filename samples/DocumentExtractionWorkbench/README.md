# Document Extraction Workbench

This sample is a deterministic harness for the temporary
`Microsoft.Extensions.DocumentExtraction` contract pinned by DOC-0. It displays
one authored SVG document, fixed extracted text, page metadata, and honest
source-coordinate regions over an AspectFit image. The green `READY` region is
actually rotated in the fixture and is reported as its original polygon; the
other regions remain axis-aligned rectangles.

## Provider boundary

`DocumentExtractionWorkbenchViewModel` depends on exactly one extraction
boundary: `IDocumentExtractionClient`. Input selection and UI-thread dispatch
are separate injected abstractions. Provider identity, readiness, and
capabilities are optional diagnostic services returned from
`IDocumentExtractionClient.GetService`.

The included `DeterministicDocumentExtractionClient` accepts only the exact
embedded fixture. It validates a canonical SHA-256 before returning fixed
content, never disposes the caller's stream, and honors cancellation. Importing
a PNG or JPEG exercises the injectable input path and source preview, but
extraction fails with an actionable message rather than applying fixture
geometry to unrelated content.

To plug in a later provider:

1. Implement `IDocumentExtractionClient`.
2. Return `DocumentExtractionClientMetadata`,
   `DocumentExtractionProviderDescriptor`, and
   `DocumentExtractionProviderReadinessDescriptor` from `GetService`.
3. Replace the DI registration in `MauiProgram`; the ViewModel and overlay do
   not change. Providers are never auto-selected.

## Run

```powershell
dotnet build samples\DocumentExtractionWorkbench\DocumentExtractionWorkbench.csproj -f net10.0-windows10.0.19041.0
dotnet build samples\DocumentExtractionWorkbench\DocumentExtractionWorkbench.csproj -f net10.0-android
```

## Explicit exclusions

- No camera capture.
- No receipt, expense, or other business projection.
- No persistence or export.
- No provider auto-selection.
- No Windows OCR, ML Kit, PaddleOCR, ONNX Runtime, model packages, network
  calls, or other real providers.
- No new public task API and no changes to the pinned proposal.
