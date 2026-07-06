#include "pch.h"
#include "CoordinateOperationInfo.h"
#include "CoordinateOperationInfo.g.cpp"

namespace winrt::CrsKitInterop::implementation
{
    CoordinateOperationInfo::CoordinateOperationInfo(int32_t code, hstring const& name, hstring const& type,
                                                     winrt::Windows::Foundation::IReference<double> const& accuracy,
                                                     hstring const& scope, hstring const& remarks, int32_t methodCode,
                                                     hstring const& areaOfUse, hstring const& gridFiles)
        : _code{ code }, _name{ name }, _type{ type }, _accuracy{ accuracy }
        , _scope{ scope }, _remarks{ remarks }, _methodCode{ methodCode }
        , _areaOfUse{ areaOfUse }, _gridFiles{ gridFiles }
    {
    }

    int32_t CoordinateOperationInfo::Code() { return _code; }
    hstring CoordinateOperationInfo::Name() { return _name; }
    hstring CoordinateOperationInfo::Type() { return _type; }
    winrt::Windows::Foundation::IReference<double> CoordinateOperationInfo::Accuracy() { return _accuracy; }
    hstring CoordinateOperationInfo::Scope() { return _scope; }
    hstring CoordinateOperationInfo::Remarks() { return _remarks; }
    int32_t CoordinateOperationInfo::MethodCode() { return _methodCode; }
    hstring CoordinateOperationInfo::AreaOfUse() { return _areaOfUse; }
    hstring CoordinateOperationInfo::GridFiles() { return _gridFiles; }
}
