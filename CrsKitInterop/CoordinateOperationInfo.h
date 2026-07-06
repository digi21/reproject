#pragma once
#include "CoordinateOperationInfo.g.h"

namespace winrt::CrsKitInterop::implementation
{
    struct CoordinateOperationInfo : CoordinateOperationInfoT<CoordinateOperationInfo>
    {
        CoordinateOperationInfo() = default;
        CoordinateOperationInfo(int32_t code, hstring const& name, hstring const& type,
                                winrt::Windows::Foundation::IReference<double> const& accuracy,
                                hstring const& scope, hstring const& remarks, int32_t methodCode,
                                hstring const& areaOfUse, hstring const& gridFiles);

        int32_t Code();
        hstring Name();
        hstring Type();
        winrt::Windows::Foundation::IReference<double> Accuracy();
        hstring Scope();
        hstring Remarks();
        int32_t MethodCode();
        hstring AreaOfUse();
        hstring GridFiles();

    private:
        int32_t _code{};
        hstring _name;
        hstring _type;
        winrt::Windows::Foundation::IReference<double> _accuracy{ nullptr };
        hstring _scope;
        hstring _remarks;
        int32_t _methodCode{};
        hstring _areaOfUse;
        hstring _gridFiles;
    };
}

// No factory_implementation: CoordinateOperationInfo has no activation constructor and no
// static members, so CppWinRT generates no activation factory. It is created internally
// via winrt::make<implementation::CoordinateOperationInfo>() and only returned across the ABI.
