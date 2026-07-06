#pragma once
#include "CrsDetails.g.h"

namespace winrt::CrsKitInterop::implementation
{
    struct CrsDetails : CrsDetailsT<CrsDetails>
    {
        CrsDetails() = default;
        CrsDetails(int32_t code, hstring const& name, hstring const& kind, int32_t axisCount,
                   hstring const& areaOfUse, hstring const& datumName, hstring const& datumOrigin,
                   hstring const& primeMeridian, hstring const& ellipsoid);

        int32_t Code();
        hstring Name();
        hstring Kind();
        int32_t AxisCount();
        hstring AreaOfUse();
        hstring DatumName();
        hstring DatumOrigin();
        hstring PrimeMeridian();
        hstring Ellipsoid();

    private:
        int32_t _code{};
        hstring _name;
        hstring _kind;
        int32_t _axisCount{};
        hstring _areaOfUse;
        hstring _datumName;
        hstring _datumOrigin;
        hstring _primeMeridian;
        hstring _ellipsoid;
    };
}

// No factory_implementation: CrsDetails has no activation constructor and no static
// members, so CppWinRT generates no activation factory. It is created internally
// via winrt::make<implementation::CrsDetails>() and only returned across the ABI.
