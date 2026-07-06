namespace Reproject;

// One CRS row in the picker list/search (code + name); the display caption is precomputed.
public sealed record CrsRow(int Code, string Name, string Kind)
{
    public string Display => $"EPSG:{Code} — {Name}";
}

// Read-only descriptive metadata of a CRS, mapped off the WinRT CrsDetails so the
// view models stay free of the interop types (and can be faked in tests).
public sealed record CrsDetail(
    int Code, string Name, string Kind, int AxisCount, string AreaOfUse,
    string DatumName, string DatumOrigin, string PrimeMeridian, string Ellipsoid);

// App-side mirror of CrsKitInterop.AxisOrder (keeps the interop enum out of the VMs).
public enum CrsAxisOrder { Default, EastNorth, NorthEast }

// One candidate EPSG coordinate operation offered when several transformations exist
// between the same pair of systems. Mapped off the WinRT CoordinateOperationInfo so the
// view models stay free of the interop types. Accuracy is in metres, null if unknown.
public sealed record TransformationOption(
    int Code, string Name, string Type, double? Accuracy, string Scope, string Remarks, int MethodCode,
    string AreaOfUse, string GridFiles);

// A live coordinate transformation, abstracted from the WinRT Transformation so the
// pipeline in MainViewModel depends on an interface, not the concrete interop type.
public interface ITransformation
{
    bool IsIdentity { get; }
    string SourceName { get; }
    string TargetName { get; }
    int SourceDimension { get; }
    double[] Transform(double[] point);
}
