#pragma once
#include "CrsEngine.g.h"

namespace winrt::CrsKitInterop::implementation
{
    struct CrsEngine : CrsEngineT<CrsEngine>
    {
        CrsEngine() = default;

        static void Initialize(hstring const& sqlitePath, hstring const& dataDirectory);
        static bool IsInitialized();
        static hstring GetEpsgVersion();
        static winrt::Windows::Foundation::Collections::IVector<winrt::CrsKitInterop::CrsInfo>
            Enumerate(hstring const& kind);
        static hstring GetName(int32_t code);
        static hstring GetAreaOfUse(int32_t code);
        static hstring GetWkt(int32_t code);
        static winrt::CrsKitInterop::CrsDetails GetDetails(int32_t code);
        static hstring GetWktWithAxisOrder(int32_t code, winrt::CrsKitInterop::AxisOrder order);
        static hstring GetStandardAxisLabel(int32_t code);
        static hstring GetCompoundWkt(int32_t horizontalCode, int32_t verticalCode);
        static hstring GetNameOfWkt(hstring const& wkt);
        static hstring DescribeAxes(hstring const& wkt);
        static winrt::CrsKitInterop::Transformation CreateTransformation(int32_t sourceCode, int32_t targetCode);
        static winrt::CrsKitInterop::Transformation CreateTransformationFromWkt(
            hstring const& sourceWkt, hstring const& targetWkt);
        static winrt::Windows::Foundation::Collections::IVectorView<winrt::CrsKitInterop::CoordinateOperationInfo>
            GetCandidateOperations(hstring const& sourceWkt, hstring const& targetWkt);
        static winrt::CrsKitInterop::Transformation CreateTransformationFromWktWithOperation(
            hstring const& sourceWkt, hstring const& targetWkt, int32_t operationCode);
    };
}

namespace winrt::CrsKitInterop::factory_implementation
{
    struct CrsEngine : CrsEngineT<CrsEngine, implementation::CrsEngine>
    {
    };
}
