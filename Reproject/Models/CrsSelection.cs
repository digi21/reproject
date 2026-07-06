namespace Reproject;

// The outcome of the CRS picker: a human-readable name plus the WKT that fully
// describes the selection (single, compound, manual or favorite). The transform
// pipeline uses the WKT via CrsEngine.CreateTransformationFromWkt.
//
// Pick records HOW the selection was made (category and sub-choices) so the picker
// can be reopened on the same option with the same choices already filled in. It is
// optional: favorites saved before this existed (and hand-built WKTs) have none.
public sealed record CrsSelection(string DisplayName, string Wkt, CrsPickState? Pick = null);

// The picker's "recipe" for a selection: which category was active and the choices
// within it. Only the fields relevant to the category are set.
public sealed record CrsPickState(
    int Category,
    int? Code = null,            // list mode: the chosen CRS code
    int AxisOrder = 0,           // list mode: the axis-order combo index
    int? HorizontalCode = null,  // compound mode: horizontal CRS code
    int? VerticalCode = null,    // compound mode: vertical CRS code (null when unknown/absent)
    bool UnknownVertical = false,// compound mode: "unknown vertical" checkbox
    string ManualWkt = "");      // manual mode: the pasted/imported WKT
