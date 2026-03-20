#pragma once

#include <cstdint>

#if defined(LAYOUTLIB_EXPORTS)
#define LAYOUTLIB_API __declspec(dllexport)
#else
#define LAYOUTLIB_API __declspec(dllimport)
#endif

extern "C"
{
    [[nodiscard]] LAYOUTLIB_API int __stdcall ComputeStructuredLayout(
        std::int32_t nodeCount,
        const std::int32_t* nodeKinds,
        const std::int32_t* ownerIndices,
        float* outX,
        float* outY);

    [[nodiscard]] LAYOUTLIB_API int __stdcall ComputeCoseLayout(
        std::int32_t nodeCount,
        const std::int32_t* nodeKinds,
        const std::int32_t* labelLengths,
        const std::int32_t* ownerIndices,
        std::int32_t edgeCount,
        const std::int32_t* edgeSources,
        const std::int32_t* edgeTargets,
        const std::int32_t* edgeKinds,
        const std::int32_t* edgeWeights,
        float* outX,
        float* outY);
}
