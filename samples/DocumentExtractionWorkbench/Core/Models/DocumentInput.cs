namespace DocumentExtractionWorkbench.Core;

public enum DocumentInputKind
{
	Fixture,
	ImportedFile,
}

public sealed class DocumentInput
{
	private readonly byte[] _content;

	public DocumentInput(
		string displayName,
		string mediaType,
		ReadOnlyMemory<byte> content,
		DocumentInputKind kind)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
		ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

		if (content.IsEmpty)
		{
			throw new ArgumentException("Document content cannot be empty.", nameof(content));
		}

		DisplayName = displayName;
		MediaType = mediaType;
		_content = content.ToArray();
		Kind = kind;
	}

	public string DisplayName { get; }

	public string MediaType { get; }

	public ReadOnlyMemory<byte> Content => _content;

	public DocumentInputKind Kind { get; }

	public Stream OpenReadStream() => new MemoryStream(_content, writable: false);
}

public class DocumentWorkbenchException : Exception
{
	public DocumentWorkbenchException(string message)
		: base(message)
	{
	}

	public DocumentWorkbenchException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}

public sealed class DocumentInputException : DocumentWorkbenchException
{
	public DocumentInputException(string message)
		: base(message)
	{
	}

	public DocumentInputException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}

public sealed class UnsupportedDocumentInputException : DocumentWorkbenchException
{
	public UnsupportedDocumentInputException(string message)
		: base(message)
	{
	}

	public UnsupportedDocumentInputException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
