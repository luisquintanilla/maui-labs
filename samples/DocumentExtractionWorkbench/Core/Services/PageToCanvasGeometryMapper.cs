using Microsoft.Extensions.DocumentExtraction;

namespace DocumentExtractionWorkbench.Core;

public static class PageToCanvasGeometryMapper
{
	private const float CoordinateTolerance = 0.001f;

	public static PageToCanvasTransform CreateTransform(
		DocumentPageGeometry page,
		WorkbenchRectangle canvas)
	{
		ArgumentNullException.ThrowIfNull(page);

		if (page.Width <= 0 || page.Height <= 0)
		{
			throw new InvalidOperationException(
				"Page dimensions must be positive before geometry can be mapped.");
		}

		if (canvas.Width <= 0 || canvas.Height <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(canvas),
				"Canvas dimensions must be positive.");
		}

		if (page.CoordinateUnit is not { } coordinateUnit)
		{
			throw new InvalidOperationException(
				"The provider did not report a page coordinate unit.");
		}

		if (page.CoordinateOrigin is not { } coordinateOrigin)
		{
			throw new InvalidOperationException(
				"The provider did not report a page coordinate origin.");
		}

		var scale = Math.Min(canvas.Width / page.Width, canvas.Height / page.Height);
		var renderedWidth = page.Width * scale;
		var renderedHeight = page.Height * scale;
		var offsetX = canvas.X + ((canvas.Width - renderedWidth) / 2);
		var offsetY = canvas.Y + ((canvas.Height - renderedHeight) / 2);

		return new PageToCanvasTransform(
			scale,
			offsetX,
			offsetY,
			page.Width,
			page.Height,
			coordinateUnit,
			coordinateOrigin);
	}

	public static MappedDocumentRegion MapPageRegion(
		DocumentPageGeometry page,
		DocumentRegionGeometry region,
		WorkbenchRectangle canvas)
	{
		ArgumentNullException.ThrowIfNull(region);

		var transform = CreateTransform(page, canvas);
		var polygon = region.Polygon.Select(transform.Map).ToArray();
		var bounds = GetBounds(polygon);
		var shape = IsAxisAlignedRectangle(region.Polygon)
			? DocumentRegionShape.Rectangle
			: DocumentRegionShape.Polygon;

		return new MappedDocumentRegion(
			region.PageNumber,
			region.Label,
			shape,
			polygon,
			bounds);
	}

	public static IReadOnlyList<MappedDocumentRegion> MapPage(
		DocumentPageGeometry page,
		WorkbenchRectangle canvas) =>
		page.Regions.Select(region => MapPageRegion(page, region, canvas)).ToArray();

	private static WorkbenchRectangle GetBounds(IReadOnlyList<WorkbenchPoint> polygon)
	{
		if (polygon.Count == 0)
		{
			return new WorkbenchRectangle(0, 0, 0, 0);
		}

		var left = polygon.Min(point => point.X);
		var top = polygon.Min(point => point.Y);
		var right = polygon.Max(point => point.X);
		var bottom = polygon.Max(point => point.Y);

		return new WorkbenchRectangle(left, top, right - left, bottom - top);
	}

	private static bool IsAxisAlignedRectangle(IReadOnlyList<WorkbenchPoint> polygon)
	{
		if (polygon.Count != 4)
		{
			return false;
		}

		for (var index = 0; index < polygon.Count; index++)
		{
			var current = polygon[index];
			var next = polygon[(index + 1) % polygon.Count];
			var horizontal = NearlyEqual(current.Y, next.Y) && !NearlyEqual(current.X, next.X);
			var vertical = NearlyEqual(current.X, next.X) && !NearlyEqual(current.Y, next.Y);

			if (!horizontal && !vertical)
			{
				return false;
			}
		}

		return true;
	}

	private static bool NearlyEqual(float left, float right) =>
		Math.Abs(left - right) <= CoordinateTolerance;
}
