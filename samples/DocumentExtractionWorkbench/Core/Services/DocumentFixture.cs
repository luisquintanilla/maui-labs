namespace DocumentExtractionWorkbench.Core;

public static class DocumentFixture
{
	public const string DisplayName = "DOC-H1 deterministic fixture";
	public const string MediaType = "image/svg+xml";
	public const string ResourceName = "DocumentExtractionWorkbench.document_fixture.svg";
	public const string ImageAssetName = "document_fixture.png";
	public const string ModelId = "doc-h1-fixed-v1";
	public const string ExpectedCanonicalSha256 = "6B74FECF2DB7BF74EF94B809B52249D0D795EEA5712F5C0A2B53D552D07F0D8E";
	public const float PageWidth = 600;
	public const float PageHeight = 800;

	public const string PageText = """
		DOCUMENT EXTRACTION
		WORKBENCH
		Reference: DOC-H1-001
		Deterministic text and geometry
		Page coordinates: 600 x 800 pixels
		Origin: top-left
		Fixed content. Fixed coordinates. No network or model.
		READY
		DOC-H1 provider-neutral extraction fixture
		""";

	public static DocumentInput CreateInput(ReadOnlyMemory<byte> content) =>
		new(DisplayName, MediaType, content, DocumentInputKind.Fixture);
}
