using DocumentExtractionWorkbench.Core;
using Microsoft.Maui.Graphics;

namespace DocumentExtractionWorkbench;

public sealed class DocumentOverlayDrawable : IDrawable
{
	public DocumentPageGeometry? Geometry { get; set; }

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		var geometry = Geometry;
		if (geometry is null ||
			geometry.Width <= 0 ||
			geometry.Height <= 0 ||
			geometry.CoordinateUnit is null ||
			geometry.CoordinateOrigin is null)
		{
			return;
		}

		var canvasBounds = new WorkbenchRectangle(
			dirtyRect.X,
			dirtyRect.Y,
			dirtyRect.Width,
			dirtyRect.Height);
		var regions = PageToCanvasGeometryMapper.MapPage(geometry, canvasBounds);

		canvas.StrokeSize = 3;
		canvas.FontSize = 12;
		foreach (var region in regions)
		{
			var strokeColor = region.Shape == DocumentRegionShape.Rectangle
				? Color.FromArgb("#4F46E5")
				: Color.FromArgb("#15803D");
			canvas.StrokeColor = strokeColor;
			canvas.FillColor = region.Shape == DocumentRegionShape.Rectangle
				? Color.FromRgba(79, 70, 229, 32)
				: Color.FromRgba(21, 128, 61, 40);
			canvas.FontColor = strokeColor;

			if (region.Shape == DocumentRegionShape.Rectangle)
			{
				canvas.FillRectangle(
					region.Bounds.X,
					region.Bounds.Y,
					region.Bounds.Width,
					region.Bounds.Height);
				canvas.DrawRectangle(
					region.Bounds.X,
					region.Bounds.Y,
					region.Bounds.Width,
					region.Bounds.Height);
			}
			else if (region.Polygon.Count >= 3)
			{
				var path = new PathF();
				path.MoveTo(region.Polygon[0].X, region.Polygon[0].Y);
				foreach (var point in region.Polygon.Skip(1))
				{
					path.LineTo(point.X, point.Y);
				}

				path.Close();
				canvas.FillPath(path);
				canvas.DrawPath(path);
			}

			if (region.Polygon.Count > 0)
			{
				canvas.DrawString(
					region.Label,
					region.Bounds.X + 4,
					Math.Max(dirtyRect.Y + 14, region.Bounds.Y + 14),
					HorizontalAlignment.Left);
			}
		}
	}
}
