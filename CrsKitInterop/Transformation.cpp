#include "pch.h"
#include "Transformation.h"
#include "Transformation.g.cpp"

using namespace CrsKit::CoordinateSystems;
using namespace CrsKit::CoordinateTransformations;

namespace winrt::CrsKitInterop::implementation
{
    Transformation::Transformation(std::shared_ptr<ICoordinateTransformation> const& transform)
        : _ct{ transform }
        , _math{ transform ? transform->GetMathTransform() : nullptr }
    {
        if (!_ct || !_math)
            throw winrt::hresult_error(
                E_FAIL, L"Could not build a transformation between the selected coordinate systems.");
    }

    hstring Transformation::SourceName()
    {
        auto const cs = _ct->GetSourceCS();
        return cs ? winrt::to_hstring(cs->GetName()) : hstring{};
    }

    hstring Transformation::TargetName()
    {
        auto const cs = _ct->GetTargetCS();
        return cs ? winrt::to_hstring(cs->GetName()) : hstring{};
    }

    int32_t Transformation::SourceDimension() { return _math->GetSourceDimension(); }
    int32_t Transformation::TargetDimension() { return _math->GetTargetDimension(); }
    bool Transformation::IsIdentity() { return _math->GetIsIdentity(); }

    com_array<double> Transformation::Transform(array_view<double const> point)
    {
        std::vector<double> const in{ point.begin(), point.end() };
        auto const out = _math->Transform(in);
        return com_array<double>{ out.begin(), out.end() };
    }

    com_array<double> Transformation::TransformPoints(array_view<double const> flatPoints)
    {
        std::span<double const> const source{ flatPoints.data(), flatPoints.size() };
        auto const out = _math->TransformPoints(source);
        return com_array<double>{ out.begin(), out.end() };
    }
}
