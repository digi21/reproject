#include "pch.h"
#include "CrsDetails.h"
#include "CrsDetails.g.cpp"

namespace winrt::CrsKitInterop::implementation
{
    CrsDetails::CrsDetails(int32_t code, hstring const& name, hstring const& kind, int32_t axisCount,
                           hstring const& areaOfUse, hstring const& datumName, hstring const& datumOrigin,
                           hstring const& primeMeridian, hstring const& ellipsoid)
        : _code{ code }, _name{ name }, _kind{ kind }, _axisCount{ axisCount }, _areaOfUse{ areaOfUse }
        , _datumName{ datumName }, _datumOrigin{ datumOrigin }, _primeMeridian{ primeMeridian }
        , _ellipsoid{ ellipsoid }
    {
    }

    int32_t CrsDetails::Code() { return _code; }
    hstring CrsDetails::Name() { return _name; }
    hstring CrsDetails::Kind() { return _kind; }
    int32_t CrsDetails::AxisCount() { return _axisCount; }
    hstring CrsDetails::AreaOfUse() { return _areaOfUse; }
    hstring CrsDetails::DatumName() { return _datumName; }
    hstring CrsDetails::DatumOrigin() { return _datumOrigin; }
    hstring CrsDetails::PrimeMeridian() { return _primeMeridian; }
    hstring CrsDetails::Ellipsoid() { return _ellipsoid; }
}
