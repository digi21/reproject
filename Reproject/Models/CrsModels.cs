namespace Reproject;

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
