# NX-Automation: Swept Geometry Generator

C# based NX Open automation script to generate perpendicular swept geometries on dynamic 3D spline paths using vector mathematics.

## 🚀 Overview
This project automates the creation of swept features in Siemens NX. It calculates local coordinate systems (LCS) dynamically along a spline to ensure the profile is always perpendicular to the path.

## 🧠 Technical Implementation
- **Spline Path:** Generates a Studio Spline through dynamic 3D points.
- **Vector Math:** Calculates Tangent Vectors at the start of the curve.
- **LCS Matrix:** Uses cross products to establish an orthogonal 3x3 orientation matrix.
- **Automation:** Creates a Datum Plane, Sketch, and Swept Feature via the NX Open API.

## 🛠️ How to Run
1. Open Siemens NX.
2. Reference `NXOpen.dll`, `NXOpen.Utilities.dll`, and `NXOpen.Features.dll`.
3. Run the script via the Journal Player or compile as a .dll.
