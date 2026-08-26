using System.Reflection;
using DocumentExtractionWorkbench.Core;
using Microsoft.Maui.Storage;

namespace DocumentExtractionWorkbench;

public sealed class MauiDocumentInputService : IDocumentInputService
{
	private const int MaximumInputBytes = 12 * 1024 * 1024;
	private readonly IFilePicker _filePicker;
	private readonly Assembly _assembly;

	public MauiDocumentInputService(IFilePicker filePicker, Assembly assembly)
	{
		_filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
		_assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
	}

	public async Task<DocumentInput> LoadFixtureAsync(
		CancellationToken cancellationToken = default)
	{
		await using var stream = _assembly.GetManifestResourceStream(DocumentFixture.ResourceName)
			?? throw new DocumentInputException(
				$"Embedded fixture '{DocumentFixture.ResourceName}' was not found.");
		var content = await ReadContentAsync(stream, cancellationToken).ConfigureAwait(false);
		return DocumentFixture.CreateInput(content);
	}

	public async Task<DocumentInput?> PickFileAsync(
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var result = await _filePicker.PickAsync(new PickOptions
		{
			PickerTitle = "Select a document image",
			FileTypes = FilePickerFileType.Images,
		}).ConfigureAwait(false);

		if (result is null)
		{
			return null;
		}

		cancellationToken.ThrowIfCancellationRequested();
		var mediaType = ResolveMediaType(result);
		await using var stream = await result.OpenReadAsync().ConfigureAwait(false);
		var content = await ReadContentAsync(stream, cancellationToken).ConfigureAwait(false);

		return new DocumentInput(
			result.FileName,
			mediaType,
			content,
			DocumentInputKind.ImportedFile);
	}

	private static async Task<byte[]> ReadContentAsync(
		Stream stream,
		CancellationToken cancellationToken)
	{
		using var buffer = new MemoryStream();
		await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

		if (buffer.Length > MaximumInputBytes)
		{
			throw new DocumentInputException(
				$"The selected image is larger than {MaximumInputBytes / (1024 * 1024)} MB.");
		}

		return buffer.ToArray();
	}

	private static string ResolveMediaType(FileResult result)
	{
		var extension = Path.GetExtension(result.FileName);
		if (string.Equals(result.ContentType, "image/png", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
		{
			return "image/png";
		}

		if (string.Equals(result.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(result.ContentType, "image/jpg", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
		{
			return "image/jpeg";
		}

		throw new DocumentInputException(
			"Choose a PNG or JPEG image. Other image formats are intentionally outside this workbench.");
	}
}
