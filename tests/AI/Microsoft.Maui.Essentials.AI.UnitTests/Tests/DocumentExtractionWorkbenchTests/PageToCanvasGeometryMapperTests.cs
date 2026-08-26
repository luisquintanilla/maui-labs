using DocumentExtractionWorkbench.Core;
using Microsoft.Extensions.DocumentExtraction;
using Xunit;

namespace Microsoft.Maui.Essentials.AI.UnitTests;

public sealed class PageToCanvasGeometryMapperTests
{
	[Fact]
	public void CreateTransform_UsesAspectFitScaleWithHorizontalAndVerticalOffsets()
	{
		var page = CreatePage(
			width: 600,
			height: 800,
			DocumentCoordinateUnit.Pixel,
			DocumentCoordinateOrigin.TopLeft);
		var canvas = new WorkbenchRectangle(10, 20, 1000, 300);

		var transform = PageToCanvasGeometryMapper.CreateTransform(page, canvas);

		Assert.Equal(0.375f, transform.Scale);
		Assert.Equal(397.5f, transform.OffsetX);
		Assert.Equal(20f, transform.OffsetY);
		Assert.Equal(600f, transform.SourceWidth);
		Assert.Equal(800f, transform.SourceHeight);
		Assert.Equal(new WorkbenchPoint(397.5f, 20f), transform.Map(new WorkbenchPoint(0, 0)));
	}

	[Fact]
	public void MapPageRegion_MapsTopLeftPixelsAndBottomLeftPointsWithExplicitUnits()
	{
		var canvas = new WorkbenchRectangle(0, 0, 200, 200);
		var region = new DocumentRegionGeometry(
			1,
			"single point",
			[new WorkbenchPoint(10, 20)]);
		var topLeftPage = CreatePage(
			width: 100,
			height: 200,
			DocumentCoordinateUnit.Pixel,
			DocumentCoordinateOrigin.TopLeft);
		var bottomLeftPage = CreatePage(
			width: 100,
			height: 200,
			DocumentCoordinateUnit.Point,
			DocumentCoordinateOrigin.BottomLeft);

		var topLeftTransform = PageToCanvasGeometryMapper.CreateTransform(topLeftPage, canvas);
		var bottomLeftTransform = PageToCanvasGeometryMapper.CreateTransform(bottomLeftPage, canvas);
		var topLeft = PageToCanvasGeometryMapper.MapPageRegion(topLeftPage, region, canvas);
		var bottomLeft = PageToCanvasGeometryMapper.MapPageRegion(bottomLeftPage, region, canvas);

		Assert.Equal(DocumentCoordinateUnit.Pixel, topLeftTransform.CoordinateUnit);
		Assert.Equal(DocumentCoordinateOrigin.TopLeft, topLeftTransform.CoordinateOrigin);
		Assert.Equal(new WorkbenchPoint(60, 20), Assert.Single(topLeft.Polygon));
		Assert.Equal(DocumentCoordinateUnit.Point, bottomLeftTransform.CoordinateUnit);
		Assert.Equal(DocumentCoordinateOrigin.BottomLeft, bottomLeftTransform.CoordinateOrigin);
		Assert.Equal(new WorkbenchPoint(60, 180), Assert.Single(bottomLeft.Polygon));
	}

	[Fact]
	public void MapPageRegion_PreservesSkewedPolygonVerticesAndTheirOrder()
	{
		var page = CreatePage(
			width: 100,
			height: 100,
			DocumentCoordinateUnit.Pixel,
			DocumentCoordinateOrigin.TopLeft);
		var polygon = new[]
		{
			new WorkbenchPoint(10, 20),
			new WorkbenchPoint(70, 10),
			new WorkbenchPoint(80, 60),
			new WorkbenchPoint(20, 70),
		};
		var region = new DocumentRegionGeometry(3, "skewed", polygon);

		var mapped = PageToCanvasGeometryMapper.MapPageRegion(
			page,
			region,
			new WorkbenchRectangle(0, 0, 100, 100));

		Assert.Equal(DocumentRegionShape.Polygon, mapped.Shape);
		Assert.Equal(polygon, mapped.Polygon);
		Assert.Equal(new WorkbenchRectangle(10, 10, 70, 60), mapped.Bounds);
	}

	[Fact]
	public void MapPageRegion_ClassifiesAxisAlignedRectangleWithoutInventingRotation()
	{
		var page = CreatePage(
			width: 100,
			height: 100,
			DocumentCoordinateUnit.Pixel,
			DocumentCoordinateOrigin.TopLeft);
		var rectangleVertices = new[]
		{
			new WorkbenchPoint(10, 20),
			new WorkbenchPoint(70, 20),
			new WorkbenchPoint(70, 60),
			new WorkbenchPoint(10, 60),
		};
		var region = new DocumentRegionGeometry(1, "rectangle", rectangleVertices);

		var mapped = PageToCanvasGeometryMapper.MapPageRegion(
			page,
			region,
			new WorkbenchRectangle(0, 0, 100, 100));

		Assert.Equal(DocumentRegionShape.Rectangle, mapped.Shape);
		Assert.Equal(rectangleVertices, mapped.Polygon);
		Assert.Equal(new WorkbenchRectangle(10, 20, 60, 40), mapped.Bounds);
	}

	[Fact]
	public void MapPageRegion_DoesNotExpandShortPolygonIntoRectangleVertices()
	{
		var page = CreatePage(
			width: 100,
			height: 100,
			DocumentCoordinateUnit.Pixel,
			DocumentCoordinateOrigin.TopLeft);
		var triangle = new[]
		{
			new WorkbenchPoint(10, 20),
			new WorkbenchPoint(70, 20),
			new WorkbenchPoint(40, 60),
		};
		var region = new DocumentRegionGeometry(1, "triangle", triangle);

		var mapped = PageToCanvasGeometryMapper.MapPageRegion(
			page,
			region,
			new WorkbenchRectangle(0, 0, 100, 100));

		Assert.Equal(DocumentRegionShape.Polygon, mapped.Shape);
		Assert.Equal(3, mapped.Polygon.Count);
		Assert.Equal(triangle, mapped.Polygon);
		Assert.Equal(new WorkbenchRectangle(10, 20, 60, 40), mapped.Bounds);
	}

	[Fact]
	public void CreateTransform_MissingCoordinateMetadata_ThrowsInsteadOfGuessing()
	{
		var canvas = new WorkbenchRectangle(0, 0, 100, 100);
		var missingUnit = new DocumentPageGeometry(
			1,
			100,
			100,
			CoordinateUnit: null,
			DocumentCoordinateOrigin.TopLeft,
			[]);
		var missingOrigin = new DocumentPageGeometry(
			1,
			100,
			100,
			DocumentCoordinateUnit.Pixel,
			CoordinateOrigin: null,
			[]);

		var unitException = Assert.Throws<InvalidOperationException>(
			() => PageToCanvasGeometryMapper.CreateTransform(missingUnit, canvas));
		var originException = Assert.Throws<InvalidOperationException>(
			() => PageToCanvasGeometryMapper.CreateTransform(missingOrigin, canvas));

		Assert.Equal("The provider did not report a page coordinate unit.", unitException.Message);
		Assert.Equal("The provider did not report a page coordinate origin.", originException.Message);
	}

	private static DocumentPageGeometry CreatePage(
		float width,
		float height,
		DocumentCoordinateUnit unit,
		DocumentCoordinateOrigin origin) =>
		new(1, width, height, unit, origin, []);
}
