using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using NXOpen;
using NXOpen.Features;
using static System.Collections.Specialized.BitVector32;

/*
 * Project: Automated Vibroacoustic Geometry Generator
 * Author: Rajat Mishra
 * Description: 
 * This NX Open script automates the creation of a swept cylinder on a 3D Studio Spline.
 * Key Engineering Logic:
 * 1. Generates a Studio Spline through dynamic 3D points.
 * 2. Calculates the Tangent Vector at the start of the spline (Scalar 0.0).
 * 3. Uses Vector Cross Products to establish an Orthogonal Coordinate System (Matrix3x3).
 * 4. Creates a Perpendicular Datum Plane and Sketch at the spline's origin.
 * 5. Automates the Swept Feature (Profile + Guide) to create a solid body.
 */

namespace ClassLibrary2
{
	public class Class1
	{
		public static void Main(string[] args)
		{
			Session thesession = Session.GetSession();
			Part part = thesession.Parts.Work;

			// --- SECTION 1: POINT CREATION ---
			// Defining global coordinates for the spline path
			Point3d coords1 = new Point3d(0.0, 10.0, 100.0);
			Point pt1 = part.Points.CreatePoint(coords1);

			Point3d coords2 = new Point3d(50.0, 20.0, 10.0);
			Point pt2 = part.Points.CreatePoint(coords2);

			Point3d coords3 = new Point3d(120.0, -10.0, 25.0);
			Point pt3 = part.Points.CreatePoint(coords3);

			Point3d coords4 = new Point3d(200.0, 30.0, 40.0);
			Point pt4 = part.Points.CreatePoint(coords4);

			// --- SECTION 2: STUDIO SPLINE GENERATION ---
			StudioSplineBuilderEx studioSplineBuilder = part.Features.CreateStudioSplineBuilderEx(null);
			studioSplineBuilder.Type = StudioSplineBuilderEx.Types.ThroughPoints;

			// Appending Geometric Constraints for each point
			GeometricConstraintData[] pointsData = new GeometricConstraintData[4];
			Point[] splinePoints = { pt1, pt2, pt3, pt4 };

			foreach (Point p in splinePoints)
			{
				GeometricConstraintData data = studioSplineBuilder.ConstraintManager.CreateGeometricConstraintData();
				data.Point = p;
				studioSplineBuilder.ConstraintManager.Append(data);
			}

			Feature studioSplineFeature = (Feature)studioSplineBuilder.Commit();
			studioSplineBuilder.Destroy();

			// Extracting Geometry from Feature for vector analysis
			StudioSpline realSpline = (StudioSpline)studioSplineFeature;
			Spline splineGeometry = (Spline)realSpline.GetEntities()[0];

			// --- SECTION 3: VECTOR MATHEMATICS (TANGENT & ORIENTATION) ---
			// Finding the direction of the curve at the start (Scalar 0.0)
			Scalar startScalar = part.Scalars.CreateScalar(0.0, Scalar.DimensionalityType.None, SmartObject.UpdateOption.WithinModeling);
			Direction tangentDir = part.Directions.CreateDirection(splineGeometry, startScalar, Direction.OnCurveOption.Tangent, Sense.Forward, SmartObject.UpdateOption.WithinModeling);
			Vector3d tVec = tangentDir.Vector;

			// Orthogonal Vector Logic: Creating X and Y axes perpendicular to the Spline Tangent (Z-axis)
			Vector3d zAxis = tVec;
			Vector3d temp = (Math.Abs(zAxis.Z) < 0.9) ? new Vector3d(0, 0, 1) : new Vector3d(1, 0, 0);

			// Cross Product for X-axis (X = Temp x Z)
			Vector3d xAxis = new Vector3d(temp.Y * zAxis.Z - temp.Z * zAxis.Y, temp.Z * zAxis.X - temp.X * zAxis.Z, temp.X * zAxis.Y - temp.Y * zAxis.X);
			// Cross Product for Y-axis (Y = Z x X)
			Vector3d yAxis = new Vector3d(zAxis.Y * xAxis.Z - zAxis.Z * xAxis.Y, zAxis.Z * xAxis.X - zAxis.X * xAxis.Z, zAxis.X * xAxis.Y - zAxis.Y * xAxis.X);

			// Mapping to NX Matrix3x3
			Matrix3x3 matrix3X3;
			matrix3X3.Xx = xAxis.X; matrix3X3.Xy = xAxis.Y; matrix3X3.Xz = xAxis.Z;
			matrix3X3.Yx = yAxis.X; matrix3X3.Yy = yAxis.Y; matrix3X3.Yz = yAxis.Z;
			matrix3X3.Zx = zAxis.X; matrix3X3.Zy = zAxis.Y; matrix3X3.Zz = zAxis.Z;

			// --- SECTION 4: DATUM PLANE & SKETCH SETUP ---
			DatumPlane datumPlane = part.Datums.CreateFixedDatumPlane(coords1, matrix3X3);
			Point3d axisEnd = new Point3d(coords1.X + matrix3X3.Xx, coords1.Y + matrix3X3.Xy, coords1.Z + matrix3X3.Xz);
			DatumAxis datumAxis = part.Datums.CreateFixedDatumAxis(coords1, axisEnd);

			SketchInPlaceBuilder skBuilder = part.Sketches.CreateNewSketchInPlaceBuilder(null);
			skBuilder.PlaneOrFace.Value = datumPlane;
			skBuilder.Axis.Value = datumAxis;
			skBuilder.SketchOrigin = pt1;

			Sketch sketch = (Sketch)skBuilder.Commit();
			skBuilder.Destroy();
			sketch.Activate(Sketch.ViewReorient.True);

			// --- SECTION 5: CIRCLE (CROSS-SECTION) GEOMETRY ---
			double radius = 10.0;
			Arc circle = part.Curves.CreateArc(coords1, sketch.Orientation, radius, 0.0, 2 * Math.PI);
			sketch.AddGeometry(circle, Sketch.InferConstraintsOption.InferCoincidentConstraints);

			// --- SECTION 6: SWEPT FEATURE (SOLID MODELING) ---
			//////////////////////////////////////////////////////////////////////////////////////////////////////

			NXOpen.Section secCircle = part.Sections.CreateSection(0.01, 0.01, 0.01);
			SelectionIntentRule[] circleRules = new SelectionIntentRule[] { part.ScRuleFactory.CreateRuleCurveDumb(new Curve[] { circle }) };
			secCircle.AddToSection(circleRules, circle, null, null, new Point3d(0, 0, 0), NXOpen.Section.Mode.Create);

			NXOpen.Section secSpline = part.Sections.CreateSection(0.01, 0.01, 0.01);
			SelectionIntentRule[] splineRules = new SelectionIntentRule[] { part.ScRuleFactory.CreateRuleCurveDumb(new Curve[] { splineGeometry }) };
			secSpline.AddToSection(splineRules, splineGeometry, null, null, new Point3d(0, 0, 0), NXOpen.Section.Mode.Create);

			// 2. Swept Builder

			SweptBuilder sweptBuilder = part.Features.CreateSweptBuilder(null);


			sweptBuilder.SectionList.Append(secCircle);
			sweptBuilder.GuideList.Append(secSpline);


			sweptBuilder.AlignmentMethod.SetSections(new NXOpen.Section[] { secCircle });


			//sweptBuilder.ScalingMethod.AreaLaw.AlongSpineData.SetFeatureSpine(secSpline);
			//sweptBuilder.OrientationMethod.AngularLaw.AlongSpineData.SetFeatureSpine(secSpline);

			sweptBuilder.G0Tolerance = 0.01;
			sweptBuilder.G1Tolerance = 0.5;


			sweptBuilder.Commit();
			sweptBuilder.Destroy();
		}

		public static int GetUnloadOption() => (int)Session.LibraryUnloadOption.Immediately;
	}
}