using Microsoft.Extensions.DocumentExtraction;

namespace DocumentExtractionWorkbench.Core;

public readonly record struct WorkbenchPoint(float X, float Y);

public readonly record struct WorkbenchRectangle(float X, float Y, float Width, float Height)
{
	public float Right => X + Width;

	public float Bottom => Y + Height;
}

public enum DocumentRegionShape
{
	Rectangle,
	Polygon,
}

public sealed record DocumentRegionGeometry(
	int PageNumber,
	string Label,
	IReadOnlyList<WorkbenchPoint> Polygon);

public sealed record DocumentPageGeometry(
	int PageNumber,
	float Width,
	float Height,
	DocumentCoordinateUnit? CoordinateUnit,
	DocumentCoordinateOrigin? CoordinateOrigin,
	IReadOnlyList<DocumentRegionGeometry> Regions)
{
	public static DocumentPageGeometry FromPage(DocumentPage page)
	{
		ArgumentNullException.ThrowIfNull(page);

		var dimensions = page.Dimensions;
		var regions = page.Elements
			.Select((element, index) => CreateRegion(element, index))
			.Where(region => region is not null)
			.Cast<DocumentRegionGeometry>()
			.ToArray();

		return new DocumentPageGeometry(
			page.PageNumber,
			dimensions?.Width ?? 0,
			dimensions?.Height ?? 0,
			page.CoordinateUnit,
			page.CoordinateOrigin,
			regions);
	}

	private static DocumentRegionGeometry? CreateRegion(DocumentElement element, int index)
	{
		if (element.BoundingRegion is not { } region)
		{
			return null;
		}

		var label = element switch
		{
			DocumentBlock block when block.Kind is { } kind => kind.Value,
			DocumentBlock => "block",
			_ => element.GetType().Name,
		};

		return new DocumentRegionGeometry(
			region.PageNumber,
			$"{index + 1}: {label}",
			region.Polygon.Select(point => new WorkbenchPoint(point.X, point.Y)).ToArray());
	}
}

public sealed record PageToCanvasTransform(
	float Scale,
	float OffsetX,
	float OffsetY,
	float SourceWidth,
	float SourceHeight,
	DocumentCoordinateUnit CoordinateUnit,
	DocumentCoordinateOrigin CoordinateOrigin)
{
	public WorkbenchPoint Map(WorkbenchPoint point)
	{
		var sourceY = CoordinateOrigin == DocumentCoordinateOrigin.TopLeft
			? point.Y
			: SourceHeight - point.Y;

		return new WorkbenchPoint(
			OffsetX + (point.X * Scale),
			OffsetY + (sourceY * Scale));
	}
}

public sealed record MappedDocumentRegion(
	int PageNumber,
	string Label,
	DocumentRegionShape Shape,
	IReadOnlyList<WorkbenchPoint> Polygon,
	WorkbenchRectangle Bounds);
