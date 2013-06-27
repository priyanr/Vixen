﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.Serialization;
using System.ComponentModel;
using Vixen.Sys;

namespace VixenModules.Preview.VixenPreview.Shapes
{
	[DataContract]
	public class PreviewTriangle : PreviewBaseShape
	{
		[DataMember] private PreviewPoint _point1 = new PreviewPoint(10, 10);
		[DataMember] private PreviewPoint _point2 = new PreviewPoint(10, 10);
		[DataMember] private PreviewPoint _point3 = new PreviewPoint(10, 10);
		private PreviewPoint _bottomRightPoint; //, _bottomPoint, _topPoint;

		private PreviewPoint p1Start, p2Start, p3Start, pBottomRightStart; //pTopStart, pBottomStart, 

		public PreviewTriangle(PreviewPoint point1, ElementNode selectedNode)
		{
			_bottomRightPoint = new PreviewPoint(point1);
			_point1 = new PreviewPoint(point1);
			_point2 = new PreviewPoint(point1);
			_point3 = new PreviewPoint(point1);

			_strings = new List<PreviewBaseShape>();

			if (selectedNode != null) {
				//List<ElementNode> children = selectedNode.Children.ToList();
				List<ElementNode> children = PreviewTools.GetLeafNodes(selectedNode);
				if (children.Count >= 6)
					//int childCount = PreviewTools.CountChildElementsInNode(selectedNode);
					//if (childCount >= 6)
				{
					int increment = children.Count/3;
					int pixelsLeft = children.Count;
					//int increment = childCount / 3;
					//int pixelsLeft = childCount;

					StringType = StringTypes.Pixel;

					// Just add lines, they will be layed out in Layout()
					for (int i = 0; i < 3; i++) {
						PreviewLine line;
						if (pixelsLeft >= increment) {
							line = new PreviewLine(new PreviewPoint(10, 10), new PreviewPoint(20, 20), increment, null);
						}
						else {
							line = new PreviewLine(new PreviewPoint(10, 10), new PreviewPoint(20, 20), pixelsLeft, null);
						}
						line.PixelColor = Color.White;
						_strings.Add(line);

						pixelsLeft -= increment;
					}

					int pixelNum = 0;
					foreach (PreviewPixel pixel in Pixels) {
						pixel.Node = children[pixelNum];
						pixel.NodeId = children[pixelNum].Id;
						pixelNum++;
					}
				}
			}

			if (_strings.Count == 0) {
				// Just add lines, they will be layed out in Layout()
				for (int i = 0; i < 3; i++) {
					PreviewLine line;
					line = new PreviewLine(new PreviewPoint(10, 10), new PreviewPoint(20, 20), 10, selectedNode);
					line.PixelColor = Color.White;
					_strings.Add(line);
				}
			}

			Layout();

			//DoResize += new ResizeEvent(OnResize);
		}

		[OnDeserialized]
		private new void OnDeserialized(StreamingContext context)
		{
			Layout();
		}

		[CategoryAttribute("Position"),
		 DisplayName("Point 1"),
		 DescriptionAttribute("An triangle is defined by a 3 points of a rectangle. This is point 1.")]
		public Point Point1
		{
			get
			{
				Point p = new Point(_point1.X, _point1.Y);
				return p;
			}
			set
			{
				_point1.X = value.X;
				_point1.Y = value.Y;
				Layout();
			}
		}

		[CategoryAttribute("Position"),
		 DisplayName("Bottom Right"),
		 DescriptionAttribute("An triangle is defined by a 3 points of a rectangle. This is point 2.")]
		public Point Point2
		{
			get
			{
				Point p = new Point(_point2.X, _point2.Y);
				return p;
			}
			set
			{
				_point2.X = value.X;
				_point2.Y = value.Y;
				Layout();
			}
		}

		[CategoryAttribute("Position"),
		 DisplayName("Bottom Right"),
		 DescriptionAttribute("An triangle is defined by a 3 points of a rectangle. This is point 3.")]
		public Point Point3
		{
			get
			{
				Point p = new Point(_point3.X, _point3.Y);
				return p;
			}
			set
			{
				_point3.X = value.X;
				_point3.Y = value.Y;
				Layout();
			}
		}

		public override List<PreviewPixel> Pixels
		{
			get
			{
				List<PreviewPixel> pixels = new List<PreviewPixel>();
				if (_strings != null) {
					for (int i = 0; i < 3; i++) {
						foreach (PreviewPixel pixel in _strings[i]._pixels) {
							pixels.Add(pixel);
						}
					}
				}
				return pixels;
			}
		}

		[CategoryAttribute("Settings"),
		 DisplayName("String 1 Light Count"),
		 DescriptionAttribute("Number of pixels or lights in string 1 of the triangle.")]
		public int LightCountString1
		{
			get { return Strings[0].Pixels.Count; }
			set
			{
				(Strings[0] as PreviewLine).PixelCount = value;
				Layout();
			}
		}

		[CategoryAttribute("Settings"),
		 DisplayName("String 2 Light Count"),
		 DescriptionAttribute("Number of pixels or lights in string 2 of the triangle.")]
		public int LightCountString2
		{
			get { return Strings[1].Pixels.Count; }
			set
			{
				(Strings[1] as PreviewLine).PixelCount = value;
				Layout();
			}
		}

		[CategoryAttribute("Settings"),
		 DisplayName("String 3 Light Count"),
		 DescriptionAttribute("Number of pixels or lights in string 3 of the triangle.")]
		public int LightCountString3
		{
			get { return Strings[2].Pixels.Count; }
			set
			{
				(Strings[2] as PreviewLine).PixelCount = value;
				Layout();
			}
		}

		public int Width
		{
			get
			{
				int x = _point1.X;
				x = Math.Max(x, _point2.X);
				x = Math.Max(x, _point3.X);
				return x;
			}
		}

		public int Height
		{
			get
			{
				int y = _point1.Y;
				y = Math.Max(y, _point2.Y);
				y = Math.Max(y, _point3.Y);
				return y;
			}
		}

		public override void Layout()
		{
			(Strings[0] as PreviewLine).Point1 = Point1;
			(Strings[0] as PreviewLine).Point2 = Point2;
			(Strings[0] as PreviewLine).Layout();

			(Strings[1] as PreviewLine).Point1 = Point2;
			(Strings[1] as PreviewLine).Point2 = Point3;
			(Strings[1] as PreviewLine).Layout();

			(Strings[2] as PreviewLine).Point1 = Point3;
			(Strings[2] as PreviewLine).Point2 = Point1;
			(Strings[2] as PreviewLine).Layout();
		}

		public override void MouseMove(int x, int y, int changeX, int changeY)
		{
			// See if we're resizing
			if (_selectedPoint != null && _selectedPoint == _bottomRightPoint) {
				int left = pBottomRightStart.X;
				int right = left + changeX;
				int width = right - left;
				int top = pBottomRightStart.Y;
				int bottom = top + changeY;
				int height = bottom - top;

				_point1.X = left + (width/2);
				_point1.Y = top;
				_point2.X = right;
				_point2.Y = bottom;
				_point3.X = left;
				_point3.Y = bottom;
				Layout();
			}
			else if (_selectedPoint != null) {
				_selectedPoint.X = x;
				_selectedPoint.Y = y;
				Layout();
			}
				// If we get here, we're moving
			else {
				_point1.X = p1Start.X + changeX;
				_point2.X = p2Start.X + changeX;
				_point3.X = p3Start.X + changeX;
				_point1.Y = p1Start.Y + changeY;
				_point2.Y = p2Start.Y + changeY;
				_point3.Y = p3Start.Y + changeY;
				Layout();
			}
		}

		//private void OnResize(EventArgs e)
		//{
		//    Layout();
		//}

		public override void Select(bool selectDragPoints)
		{
			base.Select(selectDragPoints);
			connectStandardStrings = true;
		}

		public override void SelectDragPoints()
		{
			List<PreviewPoint> points = new List<PreviewPoint>();
			points.Add(_point1);
			points.Add(_point2);
			points.Add(_point3);
			SetSelectPoints(points, null);
		}

		public override bool PointInShape(PreviewPoint point)
		{
			foreach (PreviewPixel pixel in Pixels) {
				Rectangle r = new Rectangle(pixel.X - (SelectPointSize/2), pixel.Y - (SelectPointSize/2), SelectPointSize,
				                            SelectPointSize);
				if (point.X >= r.X && point.X <= r.X + r.Width && point.Y >= r.Y && point.Y <= r.Y + r.Height) {
					return true;
				}
			}
			return false;
		}

		public override void SetSelectPoint(PreviewPoint point)
		{
			if (point == null) {
				p1Start = new PreviewPoint(_point1.X, _point1.Y);
				p2Start = new PreviewPoint(_point2.X, _point2.Y);
				p3Start = new PreviewPoint(_point3.X, _point3.Y);
			}

			_selectedPoint = point;
		}

		public override void SelectDefaultSelectPoint()
		{
			// The triangle is strange. When first drawn, the default select point should be the bottom
			// right corner. This point will not be avalable again...
			_bottomRightPoint = new PreviewPoint(10, 10);
			_bottomRightPoint.X = Math.Max(_point1.X, _point2.X);
			_bottomRightPoint.X = Math.Max(_bottomRightPoint.X, _point3.X);
			_bottomRightPoint.Y = Math.Max(_point1.Y, _point2.Y);
			_bottomRightPoint.Y = Math.Max(_bottomRightPoint.Y, _point3.Y);
			_bottomRightPoint.PointType = PreviewPoint.PointTypes.None;
			_selectPoints.Add(_bottomRightPoint);
			_selectedPoint = _bottomRightPoint;
			pBottomRightStart = new PreviewPoint(_bottomRightPoint.X, _bottomRightPoint.Y);
		}

		public override void MoveTo(int x, int y)
		{
			Point topLeft = new Point();
			topLeft.X = Math.Min(_point1.X, Math.Min(_point2.X, _point3.X));
			topLeft.Y = Math.Min(_point1.Y, Math.Min(_point2.Y, _point3.Y));

			int deltaX = x - topLeft.X;
			int deltaY = y - topLeft.Y;

			_point1.X += deltaX;
			_point1.Y += deltaY;
			_point2.X += deltaX;
			_point2.Y += deltaY;
			_point3.X += deltaX;
			_point3.Y += deltaY;

			Layout();
		}

		public override void Resize(double aspect)
		{
			_point1.X = (int) (_point1.X*aspect);
			_point1.Y = (int) (_point1.Y*aspect);
			_point2.X = (int) (_point2.X*aspect);
			_point2.Y = (int) (_point2.Y*aspect);
			_point3.X = (int) (_point3.X*aspect);
			_point3.Y = (int) (_point3.Y*aspect);

			Layout();
		}

		public override void ResizeFromOriginal(double aspect)
		{
			_point1.X = p1Start.X;
			_point1.Y = p1Start.Y;
			_point2.X = p2Start.X;
			_point2.Y = p2Start.Y;
			_point3.X = p3Start.X;
			_point3.Y = p3Start.Y;
			Resize(aspect);
		}
	}
}