#pragma once
#include <unknwn.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>

#include <atomic>
#include <format>
#include <memory>
#include <mutex>
#include <span>
#include <string>
#include <vector>

// CrsKit — the native coordinate-transformation core, consumed via the vcpkg
// port 'crskit'. Its API is STL-heavy and MSVC-specific; it stays entirely
// inside this component and is never exposed across the WinRT ABI.
#include <CrsKit.h>
#include <SqliteProvider.h>
