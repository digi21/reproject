#include "pch.h"
#include "CrsEngine.h"
#include "CrsEngine.g.cpp"
#include "CrsInfo.h"
#include "CrsDetails.h"
#include "CoordinateOperationInfo.h"
#include "Transformation.h"

using namespace CrsKit;
using namespace CrsKit::CoordinateSystems;
using namespace CrsKit::CoordinateTransformations;

namespace
{
    std::once_flag g_initFlag;
    std::atomic<bool> g_initialized{ false };

    // Translate CrsKit's std::exception failures into a projected WinRT error so
    // the C# layer sees a clean managed exception instead of a crash.
    [[noreturn]] void Rethrow(std::exception const& e)
    {
        throw winrt::hresult_error(E_FAIL, winrt::to_hstring(e.what()));
    }

    void EnsureInitialized()
    {
        if (!g_initialized.load())
            throw winrt::hresult_error(
                E_FAIL, L"CrsEngine.Initialize must be called before using the coordinate engine.");
    }

    std::shared_ptr<CoordinateSystem> MakeCoordinateSystem(int code)
    {
        auto cs = GetCoordinateSystemAuthorityFactory()->CreateCoordinateSystem(code);
        if (!cs)
            throw winrt::hresult_error(
                E_FAIL, winrt::to_hstring(std::format("Unknown or unsupported EPSG coordinate system: {}.", code)));
        return cs;
    }

    // Marshal one native candidate operation into its projected read-only view.
    winrt::CrsKitInterop::CoordinateOperationInfo MakeOperationInfo(CoordinateOperation const& op)
    {
        winrt::Windows::Foundation::IReference<double> accuracy{ nullptr };
        if (op.Accuracy.has_value())
            accuracy = winrt::box_value(op.Accuracy.value())
                           .as<winrt::Windows::Foundation::IReference<double>>();

        std::string gridFiles;
        for (auto const& file : op.GridFiles)
        {
            if (!gridFiles.empty()) gridFiles += ", ";
            gridFiles += file;
        }

        return winrt::make<winrt::CrsKitInterop::implementation::CoordinateOperationInfo>(
            op.Code,
            winrt::to_hstring(op.Name),
            winrt::to_hstring(op.Type),
            accuracy,
            winrt::to_hstring(op.Scope.value_or("")),
            winrt::to_hstring(op.Remarks.value_or("")),
            op.MethodCode,
            winrt::to_hstring(op.AreaOfUse),
            winrt::to_hstring(gridFiles));
    }

    std::shared_ptr<CoordinateSystem> ApplyAxisOrder(
        std::shared_ptr<CoordinateSystem> const& cs, winrt::CrsKitInterop::AxisOrder order)
    {
        switch (order)
        {
        case winrt::CrsKitInterop::AxisOrder::EastNorth:
            return GetCoordinateSystemFactory()->ModifyWithAxisEastNorth(cs);
        case winrt::CrsKitInterop::AxisOrder::NorthEast:
            return GetCoordinateSystemFactory()->ModifyWithAxisNorthEast(cs);
        default:
            return cs;
        }
    }
}

namespace winrt::CrsKitInterop::implementation
{
    void CrsEngine::Initialize(hstring const& sqlitePath, hstring const& dataDirectory)
    {
        try
        {
            std::call_once(g_initFlag, [&]()
            {
                CrsKit::Initialize(std::make_shared<Epsg::SQliteProvider>(winrt::to_string(sqlitePath)));
                if (!dataDirectory.empty())
                    CrsKit::GetDefaultContext()->dataDirectory = winrt::to_string(dataDirectory);
                g_initialized.store(true);
            });
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    bool CrsEngine::IsInitialized()
    {
        return g_initialized.load();
    }

    hstring CrsEngine::GetEpsgVersion()
    {
        try { return winrt::to_hstring(CrsKit::GetEpsgVersion()); }
        catch (...) { return {}; }
    }

    winrt::Windows::Foundation::Collections::IVector<winrt::CrsKitInterop::CrsInfo>
        CrsEngine::Enumerate(hstring const& kind)
    {
        EnsureInitialized();
        try
        {
            auto const map = GetCoordinateSystemAuthorityFactory()->EnumerateCoordinateSystems(winrt::to_string(kind));
            auto result = winrt::single_threaded_vector<winrt::CrsKitInterop::CrsInfo>();
            for (auto const& [code, name] : map)
                result.Append(winrt::make<implementation::CrsInfo>(code, winrt::to_hstring(name), kind));
            return result;
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    hstring CrsEngine::GetName(int32_t code)
    {
        EnsureInitialized();
        try { return winrt::to_hstring(MakeCoordinateSystem(code)->GetName()); }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    hstring CrsEngine::GetAreaOfUse(int32_t code)
    {
        EnsureInitialized();
        try { return winrt::to_hstring(GetCoordinateSystemAuthorityFactory()->GetDescriptionAreaApplicationCrs(code)); }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    hstring CrsEngine::GetWkt(int32_t code)
    {
        EnsureInitialized();
        try { return winrt::to_hstring(MakeCoordinateSystem(code)->GetWkt()); }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    winrt::CrsKitInterop::CrsDetails CrsEngine::GetDetails(int32_t code)
    {
        EnsureInitialized();
        try
        {
            auto const f = GetCoordinateSystemAuthorityFactory();
            auto tryStr = [](auto fn) -> hstring
            {
                try { return winrt::to_hstring(fn()); } catch (...) { return {}; }
            };

            hstring const name = tryStr([&] { return MakeCoordinateSystem(code)->GetName(); });
            hstring const kind = tryStr([&] { return f->GetKindOfCoordinateSystem(code); });
            hstring const area = tryStr([&] { return f->GetDescriptionAreaApplicationCrs(code); });

            int32_t axisCount{};
            try
            {
                axisCount = static_cast<int32_t>(
                    f->GetAxisOfCoordinateSystem(f->GetCodeOfCoordinateSystemAssociatedWithCrs(code)).size());
            }
            catch (...) {}

            hstring datumName, datumOrigin, meridian, ellipsoid;
            try
            {
                int const datumCode = f->GetCodeOfDatumAssociatedWithCrs(code);
                datumName = tryStr([&] { return f->GetNameOfDatum(datumCode); });
                datumOrigin = tryStr([&] { return f->GetOriginDescriptionOfDatum(datumCode); });
                ellipsoid = tryStr([&] { return f->GetNameOfEllipsoid(f->GetCodeOfEllipsoidAssociatedWithDatum(datumCode)); });
            }
            catch (...) {}
            meridian = tryStr([&] { return f->GetNameOfPrimeMeridian(f->GetCodeOfPrimeMeridianAssociatedWithCrs(code)); });

            return winrt::make<implementation::CrsDetails>(
                code, name, kind, axisCount, area, datumName, datumOrigin, meridian, ellipsoid);
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    hstring CrsEngine::GetWktWithAxisOrder(int32_t code, winrt::CrsKitInterop::AxisOrder order)
    {
        EnsureInitialized();
        try { return winrt::to_hstring(ApplyAxisOrder(MakeCoordinateSystem(code), order)->GetWkt()); }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    hstring CrsEngine::GetStandardAxisLabel(int32_t code)
    {
        EnsureInitialized();
        try
        {
            auto const cs = MakeCoordinateSystem(code);
            // The horizontal order is defined by the first two axes; use their real
            // catalogue names (EPSG abbreviations) rather than a hard-coded mapping.
            int const dimension = cs->GetDimension();
            int const count = dimension < 2 ? dimension : 2;
            std::string label;
            for (int i = 0; i < count; ++i)
            {
                std::string name;
                try { name = cs->GetAxis(i).GetName(); } catch (...) {}
                if (name.empty()) continue;
                if (!label.empty()) label += "-";
                label += name;
            }
            return winrt::to_hstring(label);
        }
        catch (...) { return {}; }
    }

    hstring CrsEngine::GetCompoundWkt(int32_t horizontalCode, int32_t verticalCode)
    {
        EnsureInitialized();
        try
        {
            auto const compound =
                GetCoordinateSystemAuthorityFactory()->CreateCompoundCoordinateSystem(horizontalCode, verticalCode);
            if (!compound)
                throw winrt::hresult_error(E_FAIL, L"Could not build the compound coordinate system.");
            return winrt::to_hstring(compound->GetWkt());
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    hstring CrsEngine::GetNameOfWkt(hstring const& wkt)
    {
        EnsureInitialized();
        try
        {
            auto const cs = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(wkt));
            if (!cs)
                throw winrt::hresult_error(E_FAIL, L"The WKT is not a valid coordinate reference system.");
            return winrt::to_hstring(cs->GetName());
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    hstring CrsEngine::DescribeAxes(hstring const& wkt)
    {
        EnsureInitialized();
        try
        {
            auto const cs = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(wkt));
            if (!cs)
                throw winrt::hresult_error(E_FAIL, L"The WKT is not a valid coordinate reference system.");

            auto unitName = [](AnyUnit const& u) -> std::string
            {
                return std::visit([](auto const& x) { return x.GetName(); }, u);
            };

            std::string result;
            int const dimension = cs->GetDimension();
            for (int i = 0; i < dimension; ++i)
            {
                std::string axisName, unit;
                try { axisName = cs->GetAxis(i).GetName(); } catch (...) {}
                try { unit = unitName(cs->GetUnits(i)); } catch (...) {}
                if (axisName.empty()) axisName = std::format("Axis {}", i + 1);

                if (!result.empty()) result += ", ";
                result += axisName;
                if (!unit.empty()) result += std::format(" ({})", unit);
            }
            return winrt::to_hstring(result);
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    winrt::CrsKitInterop::Transformation CrsEngine::CreateTransformation(int32_t sourceCode, int32_t targetCode)
    {
        EnsureInitialized();
        try
        {
            auto const source = MakeCoordinateSystem(sourceCode);
            auto const target = MakeCoordinateSystem(targetCode);
            auto const ct = GetCoordinateTransformationFactory()->CreateFromCoordinateSystems(source, target);
            return winrt::make<implementation::Transformation>(ct);
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    winrt::CrsKitInterop::Transformation CrsEngine::CreateTransformationFromWkt(
        hstring const& sourceWkt, hstring const& targetWkt)
    {
        EnsureInitialized();
        try
        {
            auto const source = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(sourceWkt));
            auto const target = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(targetWkt));
            auto const ct = GetCoordinateTransformationFactory()->CreateFromCoordinateSystems(source, target);
            return winrt::make<implementation::Transformation>(ct);
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    winrt::Windows::Foundation::Collections::IVectorView<winrt::CrsKitInterop::CoordinateOperationInfo>
        CrsEngine::GetCandidateOperations(hstring const& sourceWkt, hstring const& targetWkt)
    {
        EnsureInitialized();
        try
        {
            auto const source = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(sourceWkt));
            auto const target = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(targetWkt));

            // CrsKit invokes selectOperation only when more than one operation applies. We use it
            // purely to CAPTURE the candidate list (the transform it then builds is discarded).
            std::vector<CoordinateOperation> captured;
            bool capturedOk{ false };
            CoordinateTransformationOptions options{};
            options.selectOperation =
                [&captured, &capturedOk](std::string const&, std::string const&,
                                         std::vector<CoordinateOperation> const& ops) -> int
                {
                    captured = ops;
                    capturedOk = true;
                    return ops.empty() ? 0 : ops.front().Code; // let the throwaway build complete
                };

            try
            {
                (void)GetCoordinateTransformationFactory()->CreateFromCoordinateSystems(source, target, options);
            }
            catch (...)
            {
                // Once the list is captured, ignore a build failure of the front operation (a
                // different candidate may be usable). Only propagate if we never got the list.
                if (!capturedOk) throw;
            }

            auto result = winrt::single_threaded_vector<winrt::CrsKitInterop::CoordinateOperationInfo>();
            for (auto const& op : captured)
                result.Append(MakeOperationInfo(op));
            return result.GetView();
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }

    winrt::CrsKitInterop::Transformation CrsEngine::CreateTransformationFromWktWithOperation(
        hstring const& sourceWkt, hstring const& targetWkt, int32_t operationCode)
    {
        EnsureInitialized();
        try
        {
            auto const source = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(sourceWkt));
            auto const target = GetCoordinateSystemFactory()->CreateFromWkt(winrt::to_string(targetWkt));

            CoordinateTransformationOptions options{};
            options.selectOperation =
                [operationCode](std::string const&, std::string const&,
                                std::vector<CoordinateOperation> const&) -> int { return operationCode; };

            auto const ct = GetCoordinateTransformationFactory()->CreateFromCoordinateSystems(source, target, options);
            return winrt::make<implementation::Transformation>(ct);
        }
        catch (winrt::hresult_error const&) { throw; }
        catch (std::exception const& e) { Rethrow(e); }
    }
}
