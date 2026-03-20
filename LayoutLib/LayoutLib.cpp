#include "pch.h"
#include "LayoutLib.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <span>
#include <thread>
#include <utility>
#include <vector>

namespace
{
    constexpr std::int32_t ProjectKind = 0;
    constexpr std::int32_t DocumentKind = 1;
    constexpr std::int32_t SymbolKind = 2;
    constexpr std::int32_t PackageKind = 3;
    constexpr std::int32_t AssemblyKind = 4;
    constexpr std::int32_t DllKind = 5;

    constexpr std::int32_t ContainsDocumentEdgeKind = 0;
    constexpr std::int32_t ContainsSymbolEdgeKind = 1;
    constexpr std::int32_t ProjectReferenceEdgeKind = 2;
    constexpr std::int32_t DocumentReferenceEdgeKind = 3;
    constexpr std::int32_t SymbolCallEdgeKind = 4;
    constexpr std::int32_t SymbolInheritanceEdgeKind = 5;
    constexpr std::int32_t SymbolImplementationEdgeKind = 6;
    constexpr std::int32_t SymbolCreationEdgeKind = 7;
    constexpr std::int32_t ProjectPackageEdgeKind = 9;
    constexpr std::int32_t ProjectAssemblyEdgeKind = 10;
    constexpr std::int32_t ProjectDllEdgeKind = 11;
    constexpr std::int32_t MaxQuadTreeDepth = 18;

    using Int32Span = std::span<const std::int32_t>;
    using FloatSpan = std::span<float>;

    static Int32Span MakeInputSpan(const std::int32_t* values, std::size_t count)
    {
        return count == 0 ? Int32Span{} : Int32Span(values, count);
    }

    static FloatSpan MakeOutputSpan(float* values, std::size_t count)
    {
        return count == 0 ? FloatSpan{} : FloatSpan(values, count);
    }

    struct NodePosition
    {
        float X = 0.0f;
        float Y = 0.0f;
    };

    struct ForceVector
    {
        float X = 0.0f;
        float Y = 0.0f;
    };

    struct NodeMetrics
    {
        float HalfWidth = 56.0f;
        float HalfHeight = 20.0f;
        float CollisionPadding = 14.0f;
    };

    struct EdgeDefinition
    {
        std::int32_t Source = -1;
        std::int32_t Target = -1;
        float IdealLength = 100.0f;
        float Elasticity = 0.6f;
    };

    struct ComponentLayout
    {
        std::vector<std::int32_t> Nodes;
        float MinX = 0.0f;
        float MinY = 0.0f;
        float MaxX = 0.0f;
        float MaxY = 0.0f;
    };

    struct QuadCell
    {
        float MinX = 0.0f;
        float MinY = 0.0f;
        float MaxX = 0.0f;
        float MaxY = 0.0f;
        float Mass = 0.0f;
        float CenterX = 0.0f;
        float CenterY = 0.0f;
        std::int32_t PointIndex = -1;
        std::array<std::unique_ptr<QuadCell>, 4> Children{};

        [[nodiscard]] bool IsLeaf() const
        {
            return !Children[0] && !Children[1] && !Children[2] && !Children[3];
        }
    };

    static std::vector<std::vector<std::int32_t>> BuildOwnedNodes(
        Int32Span ownerIndices)
    {
        const auto nodeCount = static_cast<std::int32_t>(ownerIndices.size());
        std::vector<std::vector<std::int32_t>> ownedNodes(ownerIndices.size());
        for (std::size_t nodeIndex = 0; nodeIndex < ownerIndices.size(); ++nodeIndex)
        {
            const std::int32_t ownerIndex = ownerIndices[nodeIndex];
            if (ownerIndex >= 0 && ownerIndex < nodeCount)
            {
                ownedNodes[static_cast<std::size_t>(ownerIndex)].push_back(static_cast<std::int32_t>(nodeIndex));
            }
        }

        return ownedNodes;
    }

    static std::vector<std::int32_t> CollectNodeIndices(
        Int32Span nodeKinds,
        std::int32_t expectedKind)
    {
        std::vector<std::int32_t> indices;
        indices.reserve(nodeKinds.size());
        for (std::size_t nodeIndex = 0; nodeIndex < nodeKinds.size(); ++nodeIndex)
        {
            if (nodeKinds[nodeIndex] == expectedKind)
            {
                indices.push_back(static_cast<std::int32_t>(nodeIndex));
            }
        }

        return indices;
    }

    static void PositionProjects(
        const std::vector<std::int32_t>& projects,
        std::vector<NodePosition>& positions)
    {
        if (projects.empty())
        {
            return;
        }

        constexpr float spacing = 320.0f;
        const float startX = -((static_cast<float>(projects.size()) - 1.0f) * spacing) / 2.0f;
        for (std::size_t index = 0; index < projects.size(); ++index)
        {
            positions[static_cast<std::size_t>(projects[index])] = {
                startX + static_cast<float>(index) * spacing,
                -360.0f
            };
        }
    }

    static void PositionGridAround(
        const std::vector<std::int32_t>& nodes,
        std::int32_t maxColumns,
        float columnGap,
        float rowGap,
        float centerX,
        float startY,
        std::vector<NodePosition>& positions)
    {
        if (nodes.empty())
        {
            return;
        }

        maxColumns = std::max<std::int32_t>(1, maxColumns);
        const std::int32_t columnCount = std::min<std::int32_t>(maxColumns, static_cast<std::int32_t>(nodes.size()));
        for (std::size_t index = 0; index < nodes.size(); ++index)
        {
            const std::int32_t row = static_cast<std::int32_t>(index) / columnCount;
            const std::int32_t column = static_cast<std::int32_t>(index) % columnCount;
            const std::size_t remaining = nodes.size() - static_cast<std::size_t>(row) * static_cast<std::size_t>(columnCount);
            const float rowWidth = (static_cast<float>(std::min<std::size_t>(columnCount, remaining)) - 1.0f) * columnGap;
            const float x = centerX - rowWidth / 2.0f + static_cast<float>(column) * columnGap;
            const float y = startY + static_cast<float>(row) * rowGap;
            positions[static_cast<std::size_t>(nodes[index])] = { x, y };
        }
    }

    static void ComputeStructuredSeedLayout(
        Int32Span nodeKinds,
        Int32Span ownerIndices,
        std::vector<NodePosition>& positions)
    {
        positions.assign(nodeKinds.size(), NodePosition{});
        std::vector<std::vector<std::int32_t>> ownedNodes = BuildOwnedNodes(ownerIndices);
        std::vector<std::int32_t> projects = CollectNodeIndices(nodeKinds, ProjectKind);
        std::vector<std::int32_t> packages = CollectNodeIndices(nodeKinds, PackageKind);
        std::vector<std::int32_t> assemblies = CollectNodeIndices(nodeKinds, AssemblyKind);
        std::vector<std::int32_t> dlls = CollectNodeIndices(nodeKinds, DllKind);

        PositionProjects(projects, positions);

        for (const std::int32_t projectNodeIndex : projects)
        {
            const NodePosition projectPosition = positions[static_cast<std::size_t>(projectNodeIndex)];
            std::vector<std::int32_t> documentNodes;
            std::vector<std::int32_t> externalNodes;

            for (const std::int32_t ownedNodeIndex : ownedNodes[static_cast<std::size_t>(projectNodeIndex)])
            {
                switch (nodeKinds[ownedNodeIndex])
                {
                    case DocumentKind:
                        documentNodes.push_back(ownedNodeIndex);
                        break;
                    case PackageKind:
                    case AssemblyKind:
                    case DllKind:
                        externalNodes.push_back(ownedNodeIndex);
                        break;
                    default:
                        break;
                }
            }

            PositionGridAround(documentNodes, 6, 156.0f, 116.0f, projectPosition.X, -120.0f, positions);
            PositionGridAround(externalNodes, 5, 176.0f, 84.0f, projectPosition.X, 360.0f, positions);

            for (const std::int32_t documentNodeIndex : documentNodes)
            {
                const NodePosition documentPosition = positions[static_cast<std::size_t>(documentNodeIndex)];
                std::vector<std::int32_t> symbolNodes;
                for (const std::int32_t ownedNodeIndex : ownedNodes[static_cast<std::size_t>(documentNodeIndex)])
                {
                    if (nodeKinds[ownedNodeIndex] == SymbolKind)
                    {
                        symbolNodes.push_back(ownedNodeIndex);
                    }
                }

                const std::int32_t maxColumns = symbolNodes.size() >= 160 ? 12 : symbolNodes.size() >= 64 ? 10
                                                                                                          : 8;
                PositionGridAround(symbolNodes, maxColumns, 72.0f, 44.0f, documentPosition.X, documentPosition.Y + 88.0f, positions);
            }
        }

        std::vector<std::int32_t> unownedExternalNodes;
        unownedExternalNodes.reserve(packages.size() + assemblies.size() + dlls.size());
        for (const std::int32_t nodeIndex : packages)
        {
            if (ownerIndices[nodeIndex] < 0)
            {
                unownedExternalNodes.push_back(nodeIndex);
            }
        }

        for (const std::int32_t nodeIndex : assemblies)
        {
            if (ownerIndices[nodeIndex] < 0)
            {
                unownedExternalNodes.push_back(nodeIndex);
            }
        }

        for (const std::int32_t nodeIndex : dlls)
        {
            if (ownerIndices[nodeIndex] < 0)
            {
                unownedExternalNodes.push_back(nodeIndex);
            }
        }

        PositionGridAround(unownedExternalNodes, 6, 184.0f, 84.0f, 0.0f, 420.0f, positions);
    }

    static float ResolveNodeRepulsion(std::int32_t nodeKind)
    {
        switch (nodeKind)
        {
            case ProjectKind:
                return 26000.0f;
            case DocumentKind:
                return 16000.0f;
            case SymbolKind:
                return 4200.0f;
            default:
                return 11000.0f;
        }
    }

    static float ResolveNodeMass(std::int32_t nodeKind)
    {
        switch (nodeKind)
        {
            case ProjectKind:
                return 2.8f;
            case DocumentKind:
                return 1.8f;
            case SymbolKind:
                return 1.0f;
            default:
                return 1.3f;
        }
    }

    static NodeMetrics ResolveNodeMetrics(std::int32_t nodeKind, std::int32_t labelLength)
    {
        const float safeLabelLength = static_cast<float>(std::max<std::int32_t>(0, labelLength));
        switch (nodeKind)
        {
            case ProjectKind:
                return { 84.0f + std::min(40.0f, safeLabelLength) * 2.4f, 38.0f, 30.0f };
            case DocumentKind:
                return { 72.0f + std::min(48.0f, safeLabelLength) * 1.9f, 30.0f, 24.0f };
            case SymbolKind:
                return { 30.0f + std::min(56.0f, safeLabelLength) * 1.15f, 18.0f, 16.0f };
            case PackageKind:
            case AssemblyKind:
            case DllKind:
                return { 80.0f + std::min(48.0f, safeLabelLength) * 1.8f, 22.0f, 20.0f };
            default:
                return { 60.0f + std::min(40.0f, safeLabelLength) * 1.5f, 24.0f, 18.0f };
        }
    }

    static float ResolveVerticalAnchorStrength(std::int32_t nodeKind)
    {
        switch (nodeKind)
        {
            case ProjectKind:
                return 0.0026f;
            case DocumentKind:
                return 0.0014f;
            case SymbolKind:
                return 0.0006f;
            default:
                return 0.0012f;
        }
    }

    static float ResolveOwnerAnchorStrength(std::int32_t nodeKind)
    {
        switch (nodeKind)
        {
            case DocumentKind:
                return 0.008f;
            case SymbolKind:
                return 0.0045f;
            default:
                return 0.010f;
        }
    }

    static float ResolveEdgeIdealLength(std::int32_t edgeKind)
    {
        switch (edgeKind)
        {
            case ContainsDocumentEdgeKind:
                return 180.0f;
            case ContainsSymbolEdgeKind:
                return 96.0f;
            case ProjectReferenceEdgeKind:
                return 220.0f;
            case DocumentReferenceEdgeKind:
                return 160.0f;
            case SymbolCallEdgeKind:
                return 120.0f;
            case SymbolInheritanceEdgeKind:
            case SymbolImplementationEdgeKind:
                return 140.0f;
            case ProjectPackageEdgeKind:
            case ProjectAssemblyEdgeKind:
            case ProjectDllEdgeKind:
                return 168.0f;
            case SymbolCreationEdgeKind:
                return 128.0f;
            default:
                return 100.0f;
        }
    }

    static float ResolveEdgeElasticity(std::int32_t edgeKind)
    {
        switch (edgeKind)
        {
            case ContainsDocumentEdgeKind:
            case ContainsSymbolEdgeKind:
                return 0.9f;
            case ProjectReferenceEdgeKind:
                return 0.45f;
            default:
                return 0.6f;
        }
    }

    static std::vector<EdgeDefinition> BuildEdges(
        Int32Span edgeSources,
        Int32Span edgeTargets,
        Int32Span edgeKinds,
        Int32Span edgeWeights,
        const std::vector<NodeMetrics>& nodeMetrics,
        std::int32_t nodeCount)
    {
        std::vector<EdgeDefinition> edges;
        edges.reserve(edgeSources.size());

        for (std::size_t edgeIndex = 0; edgeIndex < edgeSources.size(); ++edgeIndex)
        {
            const std::int32_t source = edgeSources[edgeIndex];
            const std::int32_t target = edgeTargets[edgeIndex];
            if (source < 0 || source >= nodeCount || target < 0 || target >= nodeCount || source == target)
            {
                continue;
            }

            const std::int32_t edgeKind = edgeKinds[edgeIndex];
            const std::int32_t edgeWeight = std::max<std::int32_t>(1, edgeWeights[edgeIndex]);
            const float weightFactor = std::min(2.0f, 1.0f + 0.10f * std::log(static_cast<float>(edgeWeight) + 1.0f));
            const NodeMetrics& sourceMetrics = nodeMetrics[static_cast<std::size_t>(source)];
            const NodeMetrics& targetMetrics = nodeMetrics[static_cast<std::size_t>(target)];
            const float nodeSpacingAllowance =
                sourceMetrics.HalfWidth + targetMetrics.HalfWidth +
                std::max(sourceMetrics.CollisionPadding, targetMetrics.CollisionPadding);

            EdgeDefinition edge;
            edge.Source = source;
            edge.Target = target;
            edge.IdealLength = std::max(
                64.0f,
                std::max(ResolveEdgeIdealLength(edgeKind), nodeSpacingAllowance + 32.0f));
            edge.Elasticity = ResolveEdgeElasticity(edgeKind) * std::min(2.2f, weightFactor);
            edges.push_back(edge);
        }

        return edges;
    }

    static std::vector<std::vector<std::int32_t>> BuildConnectedComponents(
        std::int32_t nodeCount,
        const std::vector<EdgeDefinition>& edges)
    {
        std::vector<std::vector<std::int32_t>> adjacency(static_cast<std::size_t>(nodeCount));
        for (const EdgeDefinition& edge : edges)
        {
            adjacency[static_cast<std::size_t>(edge.Source)].push_back(edge.Target);
            adjacency[static_cast<std::size_t>(edge.Target)].push_back(edge.Source);
        }

        std::vector<std::vector<std::int32_t>> components;
        std::vector<std::uint8_t> visited(static_cast<std::size_t>(nodeCount), 0);
        std::vector<std::int32_t> stack;

        for (std::int32_t nodeIndex = 0; nodeIndex < nodeCount; ++nodeIndex)
        {
            if (visited[static_cast<std::size_t>(nodeIndex)] != 0)
            {
                continue;
            }

            components.emplace_back();
            std::vector<std::int32_t>& component = components.back();
            stack.clear();
            stack.push_back(nodeIndex);
            visited[static_cast<std::size_t>(nodeIndex)] = 1;

            while (!stack.empty())
            {
                const std::int32_t current = stack.back();
                stack.pop_back();
                component.push_back(current);

                for (const std::int32_t neighbor : adjacency[static_cast<std::size_t>(current)])
                {
                    if (visited[static_cast<std::size_t>(neighbor)] != 0)
                    {
                        continue;
                    }

                    visited[static_cast<std::size_t>(neighbor)] = 1;
                    stack.push_back(neighbor);
                }
            }
        }

        return components;
    }

    static std::int32_t ResolveQuadrant(const QuadCell& cell, const NodePosition& position)
    {
        const float midX = (cell.MinX + cell.MaxX) * 0.5f;
        const float midY = (cell.MinY + cell.MaxY) * 0.5f;
        const bool right = position.X >= midX;
        const bool bottom = position.Y >= midY;

        if (!right && !bottom)
        {
            return 0;
        }

        if (right && !bottom)
        {
            return 1;
        }

        if (!right && bottom)
        {
            return 2;
        }

        return 3;
    }

    static void EnsureChild(QuadCell& cell, std::int32_t quadrant)
    {
        if (cell.Children[quadrant])
        {
            return;
        }

        const float midX = (cell.MinX + cell.MaxX) * 0.5f;
        const float midY = (cell.MinY + cell.MaxY) * 0.5f;
        std::unique_ptr<QuadCell> child = std::make_unique<QuadCell>();
        switch (quadrant)
        {
            case 0:
                child->MinX = cell.MinX;
                child->MinY = cell.MinY;
                child->MaxX = midX;
                child->MaxY = midY;
                break;
            case 1:
                child->MinX = midX;
                child->MinY = cell.MinY;
                child->MaxX = cell.MaxX;
                child->MaxY = midY;
                break;
            case 2:
                child->MinX = cell.MinX;
                child->MinY = midY;
                child->MaxX = midX;
                child->MaxY = cell.MaxY;
                break;
            default:
                child->MinX = midX;
                child->MinY = midY;
                child->MaxX = cell.MaxX;
                child->MaxY = cell.MaxY;
                break;
        }

        cell.Children[quadrant] = std::move(child);
    }

    static void InsertPoint(
        QuadCell& cell,
        std::int32_t pointIndex,
        const std::vector<NodePosition>& positions,
        const std::vector<float>& masses,
        std::int32_t depth = 0)
    {
        const NodePosition& position = positions[static_cast<std::size_t>(pointIndex)];
        const float pointMass = masses[static_cast<std::size_t>(pointIndex)];
        if (cell.Mass <= 0.0f)
        {
            cell.CenterX = position.X;
            cell.CenterY = position.Y;
            cell.Mass = pointMass;
        }
        else
        {
            const float totalMass = cell.Mass + pointMass;
            cell.CenterX = (cell.CenterX * cell.Mass + position.X * pointMass) / totalMass;
            cell.CenterY = (cell.CenterY * cell.Mass + position.Y * pointMass) / totalMass;
            cell.Mass = totalMass;
        }

        if (cell.IsLeaf())
        {
            if (cell.PointIndex < 0)
            {
                cell.PointIndex = pointIndex;
                return;
            }

            if (depth >= MaxQuadTreeDepth)
            {
                cell.PointIndex = -1;
                return;
            }

            const std::int32_t existingPointIndex = cell.PointIndex;
            cell.PointIndex = -1;
            const std::int32_t existingQuadrant = ResolveQuadrant(cell, positions[static_cast<std::size_t>(existingPointIndex)]);
            EnsureChild(cell, existingQuadrant);
            InsertPoint(*cell.Children[existingQuadrant], existingPointIndex, positions, masses, depth + 1);
        }

        const std::int32_t quadrant = ResolveQuadrant(cell, position);
        EnsureChild(cell, quadrant);
        InsertPoint(*cell.Children[quadrant], pointIndex, positions, masses, depth + 1);
    }

    static QuadCell BuildQuadTree(
        const std::vector<NodePosition>& positions,
        const std::vector<float>& masses)
    {
        QuadCell root;
        if (positions.empty())
        {
            return root;
        }

        float minX = positions[0].X;
        float minY = positions[0].Y;
        float maxX = positions[0].X;
        float maxY = positions[0].Y;
        for (const NodePosition& position : positions)
        {
            minX = std::min(minX, position.X);
            minY = std::min(minY, position.Y);
            maxX = std::max(maxX, position.X);
            maxY = std::max(maxY, position.Y);
        }

        const float width = std::max(1.0f, maxX - minX);
        const float height = std::max(1.0f, maxY - minY);
        const float size = std::max(width, height) + 128.0f;
        const float centerX = (minX + maxX) * 0.5f;
        const float centerY = (minY + maxY) * 0.5f;

        root.MinX = centerX - size * 0.5f;
        root.MaxX = centerX + size * 0.5f;
        root.MinY = centerY - size * 0.5f;
        root.MaxY = centerY + size * 0.5f;

        for (std::size_t index = 0; index < positions.size(); ++index)
        {
            InsertPoint(root, static_cast<std::int32_t>(index), positions, masses);
        }

        return root;
    }

    static void AccumulateRepulsion(
        const QuadCell& cell,
        std::int32_t nodeIndex,
        const std::vector<NodePosition>& positions,
        float repulsionBase,
        ForceVector& force)
    {
        if (cell.Mass <= 0.0f)
        {
            return;
        }

        const NodePosition& position = positions[static_cast<std::size_t>(nodeIndex)];
        const float dx = cell.CenterX - position.X;
        const float dy = cell.CenterY - position.Y;
        const float distanceSquared = dx * dx + dy * dy + 12.0f;
        const float size = std::max(cell.MaxX - cell.MinX, cell.MaxY - cell.MinY);
        const bool useAggregate = !cell.IsLeaf() && ((size * size) / distanceSquared) < 0.64f;

        if (cell.IsLeaf() || useAggregate)
        {
            if (cell.PointIndex == nodeIndex && cell.IsLeaf())
            {
                return;
            }

            const float distance = std::sqrt(distanceSquared);
            const float magnitude = repulsionBase * cell.Mass / distanceSquared;
            force.X -= (dx / distance) * magnitude;
            force.Y -= (dy / distance) * magnitude;
            return;
        }

        for (const std::unique_ptr<QuadCell>& child : cell.Children)
        {
            if (child)
            {
                AccumulateRepulsion(*child, nodeIndex, positions, repulsionBase, force);
            }
        }
    }

    static void ComputeRepulsionForRange(
        const QuadCell& root,
        const std::vector<NodePosition>& positions,
        const std::vector<float>& repulsionBases,
        std::vector<ForceVector>& forces,
        std::size_t startIndex,
        std::size_t endIndex)
    {
        for (std::size_t nodeIndex = startIndex; nodeIndex < endIndex; ++nodeIndex)
        {
            ForceVector force;
            AccumulateRepulsion(root, static_cast<std::int32_t>(nodeIndex), positions, repulsionBases[nodeIndex], force);
            forces[nodeIndex] = force;
        }
    }

    static void ApplyEdgeAttraction(
        const std::vector<EdgeDefinition>& edges,
        const std::vector<NodePosition>& positions,
        std::vector<ForceVector>& forces)
    {
        for (const EdgeDefinition& edge : edges)
        {
            const NodePosition& sourcePosition = positions[static_cast<std::size_t>(edge.Source)];
            const NodePosition& targetPosition = positions[static_cast<std::size_t>(edge.Target)];
            const float dx = targetPosition.X - sourcePosition.X;
            const float dy = targetPosition.Y - sourcePosition.Y;
            const float distance = std::sqrt(dx * dx + dy * dy + 0.01f);
            const float spring = edge.Elasticity * 0.022f * (distance - edge.IdealLength);
            const float fx = (dx / distance) * spring;
            const float fy = (dy / distance) * spring;

            forces[static_cast<std::size_t>(edge.Source)].X += fx;
            forces[static_cast<std::size_t>(edge.Source)].Y += fy;
            forces[static_cast<std::size_t>(edge.Target)].X -= fx;
            forces[static_cast<std::size_t>(edge.Target)].Y -= fy;
        }
    }

    static void ApplyOwnerAnchors(
        const std::vector<NodePosition>& positions,
        const std::vector<NodePosition>& initialOffsets,
        const std::vector<std::int32_t>& nodeKinds,
        const std::vector<std::int32_t>& ownerIndices,
        std::vector<ForceVector>& forces)
    {
        for (std::size_t nodeIndex = 0; nodeIndex < positions.size(); ++nodeIndex)
        {
            const std::int32_t ownerIndex = ownerIndices[nodeIndex];
            if (ownerIndex < 0 || ownerIndex >= static_cast<std::int32_t>(positions.size()))
            {
                continue;
            }

            const float ownerStrength = ResolveOwnerAnchorStrength(nodeKinds[nodeIndex]);
            const NodePosition& ownerPosition = positions[static_cast<std::size_t>(ownerIndex)];
            const NodePosition& currentPosition = positions[nodeIndex];
            const NodePosition& initialOffset = initialOffsets[nodeIndex];
            const float scale = nodeKinds[nodeIndex] == SymbolKind ? 1.02f : 1.00f;
            const float targetX = ownerPosition.X + initialOffset.X * scale;
            const float targetY = ownerPosition.Y + initialOffset.Y * scale;
            forces[nodeIndex].X += (targetX - currentPosition.X) * ownerStrength;
            forces[nodeIndex].Y += (targetY - currentPosition.Y) * ownerStrength;
        }
    }

    static void ApplyGlobalAnchors(
        const std::vector<NodePosition>& positions,
        const std::vector<NodePosition>& initialPositions,
        const std::vector<std::int32_t>& nodeKinds,
        const std::vector<std::int32_t>& ownerIndices,
        std::vector<ForceVector>& forces)
    {
        for (std::size_t nodeIndex = 0; nodeIndex < positions.size(); ++nodeIndex)
        {
            const std::int32_t ownerIndex = ownerIndices[nodeIndex];
            const float verticalStrength = ResolveVerticalAnchorStrength(nodeKinds[nodeIndex]);
            if (ownerIndex < 0)
            {
                forces[nodeIndex].X += (initialPositions[nodeIndex].X - positions[nodeIndex].X) * (nodeKinds[nodeIndex] == ProjectKind ? 0.0038f : 0.0024f);
                forces[nodeIndex].Y += (initialPositions[nodeIndex].Y - positions[nodeIndex].Y) * (verticalStrength * 2.0f);
                continue;
            }

            if (nodeKinds[nodeIndex] != SymbolKind)
            {
                forces[nodeIndex].Y += (initialPositions[nodeIndex].Y - positions[nodeIndex].Y) * verticalStrength;
            }
        }
    }

    static void ResolveNodeOverlaps(
        const std::vector<NodeMetrics>& nodeMetrics,
        std::vector<NodePosition>& positions,
        std::int32_t maxPassCount)
    {
        if (positions.size() < 2 || maxPassCount <= 0)
        {
            return;
        }

        std::vector<std::int32_t> order(positions.size());
        for (std::size_t index = 0; index < positions.size(); ++index)
        {
            order[index] = static_cast<std::int32_t>(index);
        }

        for (std::int32_t pass = 0; pass < maxPassCount; ++pass)
        {
            std::ranges::sort(order, [&positions](std::int32_t left, std::int32_t right)
                              { return positions[static_cast<std::size_t>(left)].X < positions[static_cast<std::size_t>(right)].X; });

            bool movedAny = false;
            for (std::size_t leftOrderIndex = 0; leftOrderIndex < order.size(); ++leftOrderIndex)
            {
                const std::int32_t leftNodeIndex = order[leftOrderIndex];
                const NodeMetrics& leftMetrics = nodeMetrics[static_cast<std::size_t>(leftNodeIndex)];
                for (std::size_t rightOrderIndex = leftOrderIndex + 1; rightOrderIndex < order.size(); ++rightOrderIndex)
                {
                    const std::int32_t rightNodeIndex = order[rightOrderIndex];
                    const NodeMetrics& rightMetrics = nodeMetrics[static_cast<std::size_t>(rightNodeIndex)];
                    const float maxHorizontalRange =
                        leftMetrics.HalfWidth + rightMetrics.HalfWidth +
                        leftMetrics.CollisionPadding + rightMetrics.CollisionPadding + 12.0f;
                    const float deltaX = positions[static_cast<std::size_t>(rightNodeIndex)].X - positions[static_cast<std::size_t>(leftNodeIndex)].X;
                    if (deltaX > maxHorizontalRange)
                    {
                        break;
                    }

                    const float deltaY = positions[static_cast<std::size_t>(rightNodeIndex)].Y - positions[static_cast<std::size_t>(leftNodeIndex)].Y;
                    const float overlapX =
                        leftMetrics.HalfWidth + rightMetrics.HalfWidth +
                        std::max(leftMetrics.CollisionPadding, rightMetrics.CollisionPadding) -
                        std::fabs(deltaX);
                    const float overlapY =
                        leftMetrics.HalfHeight + rightMetrics.HalfHeight +
                        std::max(leftMetrics.CollisionPadding * 0.6f, rightMetrics.CollisionPadding * 0.6f) -
                        std::fabs(deltaY);

                    if (overlapX <= 0.0f || overlapY <= 0.0f)
                    {
                        continue;
                    }

                    movedAny = true;
                    const float safeDeltaX = std::fabs(deltaX) < 0.001f ? (leftNodeIndex <= rightNodeIndex ? -1.0f : 1.0f) : deltaX;
                    const float safeDeltaY = std::fabs(deltaY) < 0.001f ? ((leftNodeIndex + rightNodeIndex) % 2 == 0 ? -1.0f : 1.0f) : deltaY;
                    if (overlapX < overlapY)
                    {
                        const float separation = overlapX * 0.72f + 10.0f;
                        const float direction = safeDeltaX < 0.0f ? -1.0f : 1.0f;
                        positions[static_cast<std::size_t>(leftNodeIndex)].X -= separation * 0.5f * direction;
                        positions[static_cast<std::size_t>(rightNodeIndex)].X += separation * 0.5f * direction;
                    }
                    else
                    {
                        const float separation = overlapY * 0.72f + 8.0f;
                        const float direction = safeDeltaY < 0.0f ? -1.0f : 1.0f;
                        positions[static_cast<std::size_t>(leftNodeIndex)].Y -= separation * 0.5f * direction;
                        positions[static_cast<std::size_t>(rightNodeIndex)].Y += separation * 0.5f * direction;
                    }
                }
            }

            if (!movedAny)
            {
                break;
            }
        }
    }

    static void RunForceDirectedLayout(
        const std::vector<std::int32_t>& nodeKinds,
        const std::vector<NodeMetrics>& nodeMetrics,
        const std::vector<std::int32_t>& ownerIndices,
        const std::vector<EdgeDefinition>& edges,
        std::vector<NodePosition>& positions)
    {
        if (positions.empty())
        {
            return;
        }

        std::vector<NodePosition> initialPositions = positions;
        std::vector<NodePosition> initialOffsets(positions.size());
        std::vector<float> masses(positions.size());
        std::vector<float> repulsionBases(positions.size());
        std::vector<ForceVector> forces(positions.size());
        std::vector<ForceVector> velocities(positions.size());

        for (std::size_t nodeIndex = 0; nodeIndex < positions.size(); ++nodeIndex)
        {
            masses[nodeIndex] = ResolveNodeMass(nodeKinds[nodeIndex]);
            repulsionBases[nodeIndex] = ResolveNodeRepulsion(nodeKinds[nodeIndex]) * (0.88f + nodeMetrics[nodeIndex].HalfWidth / 120.0f);
            const std::int32_t ownerIndex = ownerIndices[nodeIndex];
            if (ownerIndex >= 0 && ownerIndex < static_cast<std::int32_t>(positions.size()))
            {
                initialOffsets[nodeIndex].X = initialPositions[nodeIndex].X - initialPositions[static_cast<std::size_t>(ownerIndex)].X;
                initialOffsets[nodeIndex].Y = initialPositions[nodeIndex].Y - initialPositions[static_cast<std::size_t>(ownerIndex)].Y;
            }
            else
            {
                initialOffsets[nodeIndex] = initialPositions[nodeIndex];
            }
        }

        const std::size_t nodeCount = positions.size();
        const std::size_t hardwareThreads = std::max<std::size_t>(1, std::thread::hardware_concurrency());
        const std::size_t workerCount = nodeCount >= 512
                                            ? std::min<std::size_t>(hardwareThreads, 8)
                                            : 1;
        const std::size_t iterationCount = nodeCount >= 2400 ? 660 : nodeCount >= 1000 ? 760
                                                                                       : 980;
        float temperature = nodeCount >= 2400 ? 24.0f : nodeCount >= 1000 ? 28.0f
                                                                          : 32.0f;
        const float finalTemperature = nodeCount >= 2400 ? 0.84f : 0.64f;
        const float cooling = iterationCount > 1
                                  ? std::pow(finalTemperature / temperature, 1.0f / static_cast<float>(iterationCount - 1))
                                  : 1.0f;

        for (std::size_t iteration = 0; iteration < iterationCount; ++iteration)
        {
            QuadCell root = BuildQuadTree(positions, masses);

            if (workerCount <= 1)
            {
                ComputeRepulsionForRange(root, positions, repulsionBases, forces, 0, nodeCount);
            }
            else
            {
                std::vector<std::thread> workers;
                workers.reserve(workerCount);
                const std::size_t chunkSize = (nodeCount + workerCount - 1) / workerCount;
                for (std::size_t workerIndex = 0; workerIndex < workerCount; ++workerIndex)
                {
                    const std::size_t startIndex = workerIndex * chunkSize;
                    const std::size_t endIndex = std::min(nodeCount, startIndex + chunkSize);
                    if (startIndex >= endIndex)
                    {
                        continue;
                    }

                    workers.emplace_back([&root, &positions, &repulsionBases, &forces, startIndex, endIndex]()
                                         { ComputeRepulsionForRange(root, positions, repulsionBases, forces, startIndex, endIndex); });
                }

                for (std::thread& worker : workers)
                {
                    worker.join();
                }
            }

            ApplyEdgeAttraction(edges, positions, forces);
            ApplyOwnerAnchors(positions, initialOffsets, nodeKinds, ownerIndices, forces);
            ApplyGlobalAnchors(positions, initialPositions, nodeKinds, ownerIndices, forces);

            float totalDisplacement = 0.0f;
            for (std::size_t nodeIndex = 0; nodeIndex < nodeCount; ++nodeIndex)
            {
                velocities[nodeIndex].X = velocities[nodeIndex].X * 0.72f + forces[nodeIndex].X * 0.28f;
                velocities[nodeIndex].Y = velocities[nodeIndex].Y * 0.72f + forces[nodeIndex].Y * 0.28f;

                const float magnitude = std::sqrt(
                    velocities[nodeIndex].X * velocities[nodeIndex].X +
                    velocities[nodeIndex].Y * velocities[nodeIndex].Y);
                if (magnitude <= 0.0001f)
                {
                    continue;
                }

                const float allowed = std::min(temperature, magnitude);
                const float scale = allowed / magnitude;
                positions[nodeIndex].X += velocities[nodeIndex].X * scale;
                positions[nodeIndex].Y += velocities[nodeIndex].Y * scale;
                totalDisplacement += allowed;
            }

            if (((iteration + 1) % 24) == 0)
            {
                ResolveNodeOverlaps(nodeMetrics, positions, nodeCount >= 1800 ? 2 : 3);
            }

            temperature *= cooling;
            if ((iteration + 1) % (nodeCount >= 1800 ? 96 : 72) == 0)
            {
                ResolveNodeOverlaps(nodeMetrics, positions, 2);
            }

            if (iteration > 32)
            {
                const float averageDisplacement = totalDisplacement / static_cast<float>(nodeCount);
                if (averageDisplacement < 0.22f)
                {
                    break;
                }
            }
        }

        ResolveNodeOverlaps(nodeMetrics, positions, nodeCount >= 1800 ? 8 : 10);
    }

    static ComponentLayout MeasureComponent(
        const std::vector<std::int32_t>& componentNodes,
        const std::vector<NodePosition>& positions,
        const std::vector<NodeMetrics>& nodeMetrics)
    {
        ComponentLayout layout;
        layout.Nodes = componentNodes;
        if (componentNodes.empty())
        {
            return layout;
        }

        const NodePosition& first = positions[static_cast<std::size_t>(componentNodes[0])];
        const NodeMetrics& firstMetrics = nodeMetrics[static_cast<std::size_t>(componentNodes[0])];
        layout.MinX = first.X - firstMetrics.HalfWidth;
        layout.MaxX = first.X + firstMetrics.HalfWidth;
        layout.MinY = first.Y - firstMetrics.HalfHeight;
        layout.MaxY = first.Y + firstMetrics.HalfHeight;

        for (const std::int32_t nodeIndex : componentNodes)
        {
            const NodePosition& position = positions[static_cast<std::size_t>(nodeIndex)];
            const NodeMetrics& metrics = nodeMetrics[static_cast<std::size_t>(nodeIndex)];
            layout.MinX = std::min(layout.MinX, position.X - metrics.HalfWidth);
            layout.MaxX = std::max(layout.MaxX, position.X + metrics.HalfWidth);
            layout.MinY = std::min(layout.MinY, position.Y - metrics.HalfHeight);
            layout.MaxY = std::max(layout.MaxY, position.Y + metrics.HalfHeight);
        }

        return layout;
    }

    static void PackComponents(
        const std::vector<std::vector<std::int32_t>>& components,
        const std::vector<NodeMetrics>& nodeMetrics,
        std::vector<NodePosition>& positions)
    {
        if (components.size() <= 1)
        {
            return;
        }

        std::vector<ComponentLayout> measured;
        measured.reserve(components.size());
        float combinedArea = 0.0f;
        for (const std::vector<std::int32_t>& component : components)
        {
            ComponentLayout layout = MeasureComponent(component, positions, nodeMetrics);
            const float width = std::max(120.0f, layout.MaxX - layout.MinX + 96.0f);
            const float height = std::max(120.0f, layout.MaxY - layout.MinY + 96.0f);
            combinedArea += width * height;
            measured.push_back(std::move(layout));
        }

        std::ranges::sort(measured, [](const ComponentLayout& left, const ComponentLayout& right)
                          { return left.Nodes.size() > right.Nodes.size(); });

        const float spacing = positions.size() >= 1000 ? 220.0f : 144.0f;
        const float targetRowWidth = std::max(640.0f, std::sqrt(combinedArea) * 1.15f);
        float cursorX = 0.0f;
        float cursorY = 0.0f;
        float rowHeight = 0.0f;

        for (ComponentLayout& layout : measured)
        {
            const float width = std::max(120.0f, layout.MaxX - layout.MinX + 96.0f);
            const float height = std::max(120.0f, layout.MaxY - layout.MinY + 96.0f);
            if (cursorX > 0.0f && cursorX + width > targetRowWidth)
            {
                cursorX = 0.0f;
                cursorY += rowHeight + spacing;
                rowHeight = 0.0f;
            }

            const float offsetX = cursorX - layout.MinX;
            const float offsetY = cursorY - layout.MinY;
            for (const std::int32_t nodeIndex : layout.Nodes)
            {
                positions[static_cast<std::size_t>(nodeIndex)].X += offsetX;
                positions[static_cast<std::size_t>(nodeIndex)].Y += offsetY;
            }

            cursorX += width + spacing;
            rowHeight = std::max(rowHeight, height);
        }
    }

    static void CenterPositions(std::vector<NodePosition>& positions)
    {
        if (positions.empty())
        {
            return;
        }

        float minX = positions[0].X;
        float maxX = positions[0].X;
        float minY = positions[0].Y;
        float maxY = positions[0].Y;
        for (const NodePosition& position : positions)
        {
            minX = std::min(minX, position.X);
            maxX = std::max(maxX, position.X);
            minY = std::min(minY, position.Y);
            maxY = std::max(maxY, position.Y);
        }

        const float centerX = (minX + maxX) * 0.5f;
        const float centerY = (minY + maxY) * 0.5f;
        for (NodePosition& position : positions)
        {
            position.X -= centerX;
            position.Y -= centerY;
        }
    }

    static bool HasExpectedSize(Int32Span values, std::size_t expectedSize)
    {
        return values.size() == expectedSize;
    }

    static bool HasExpectedSize(FloatSpan values, std::size_t expectedSize)
    {
        return values.size() == expectedSize;
    }

    static std::vector<NodeMetrics> BuildNodeMetrics(
        Int32Span nodeKinds,
        Int32Span labelLengths)
    {
        std::vector<NodeMetrics> nodeMetrics;
        nodeMetrics.reserve(nodeKinds.size());
        for (std::size_t nodeIndex = 0; nodeIndex < nodeKinds.size(); ++nodeIndex)
        {
            nodeMetrics.push_back(ResolveNodeMetrics(nodeKinds[nodeIndex], labelLengths[nodeIndex]));
        }

        return nodeMetrics;
    }

    static void WritePositions(
        const std::vector<NodePosition>& positions,
        FloatSpan outX,
        FloatSpan outY)
    {
        for (std::size_t nodeIndex = 0; nodeIndex < positions.size(); ++nodeIndex)
        {
            outX[nodeIndex] = positions[nodeIndex].X;
            outY[nodeIndex] = positions[nodeIndex].Y;
        }
    }

    static bool ComputeStructuredLayoutCore(
        Int32Span nodeKinds,
        Int32Span ownerIndices,
        FloatSpan outX,
        FloatSpan outY)
    {
        if (nodeKinds.empty() ||
            !HasExpectedSize(ownerIndices, nodeKinds.size()) ||
            !HasExpectedSize(outX, nodeKinds.size()) ||
            !HasExpectedSize(outY, nodeKinds.size()))
        {
            return false;
        }

        std::vector<NodePosition> positions;
        ComputeStructuredSeedLayout(nodeKinds, ownerIndices, positions);
        WritePositions(positions, outX, outY);
        return true;
    }

    static bool ComputeCoseLayoutCore(
        Int32Span nodeKinds,
        Int32Span labelLengths,
        Int32Span ownerIndices,
        Int32Span edgeSources,
        Int32Span edgeTargets,
        Int32Span edgeKinds,
        Int32Span edgeWeights,
        FloatSpan outX,
        FloatSpan outY)
    {
        if (nodeKinds.empty() ||
            !HasExpectedSize(labelLengths, nodeKinds.size()) ||
            !HasExpectedSize(ownerIndices, nodeKinds.size()) ||
            !HasExpectedSize(outX, nodeKinds.size()) ||
            !HasExpectedSize(outY, nodeKinds.size()) ||
            edgeSources.size() != edgeTargets.size() ||
            edgeSources.size() != edgeKinds.size() ||
            edgeSources.size() != edgeWeights.size())
        {
            return false;
        }

        std::vector<NodePosition> positions;
        ComputeStructuredSeedLayout(nodeKinds, ownerIndices, positions);

        if (!edgeSources.empty())
        {
            std::vector<std::int32_t> nodeKindVector(nodeKinds.begin(), nodeKinds.end());
            std::vector<NodeMetrics> nodeMetricVector = BuildNodeMetrics(nodeKinds, labelLengths);
            std::vector<std::int32_t> ownerIndexVector(ownerIndices.begin(), ownerIndices.end());
            std::vector<EdgeDefinition> edges = BuildEdges(
                edgeSources,
                edgeTargets,
                edgeKinds,
                edgeWeights,
                nodeMetricVector,
                static_cast<std::int32_t>(nodeKinds.size()));
            RunForceDirectedLayout(nodeKindVector, nodeMetricVector, ownerIndexVector, edges, positions);
            PackComponents(
                BuildConnectedComponents(static_cast<std::int32_t>(nodeKinds.size()), edges),
                nodeMetricVector,
                positions);
            CenterPositions(positions);
        }

        WritePositions(positions, outX, outY);
        return true;
    }
} // namespace

extern "C" LAYOUTLIB_API int __stdcall ComputeStructuredLayout(
    std::int32_t nodeCount,
    const std::int32_t* nodeKinds,
    const std::int32_t* ownerIndices,
    float* outX,
    float* outY)
{
    if (nodeCount <= 0 ||
        nodeKinds == nullptr ||
        ownerIndices == nullptr ||
        outX == nullptr ||
        outY == nullptr)
    {
        return 0;
    }

    const auto safeNodeCount = static_cast<std::size_t>(nodeCount);
    return ComputeStructuredLayoutCore(
               MakeInputSpan(nodeKinds, safeNodeCount),
               MakeInputSpan(ownerIndices, safeNodeCount),
               MakeOutputSpan(outX, safeNodeCount),
               MakeOutputSpan(outY, safeNodeCount))
               ? 1
               : 0;
}

extern "C" LAYOUTLIB_API int __stdcall ComputeCoseLayout(
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
    float* outY)
{
    if (nodeCount <= 0 ||
        nodeKinds == nullptr ||
        labelLengths == nullptr ||
        ownerIndices == nullptr ||
        outX == nullptr ||
        outY == nullptr)
    {
        return 0;
    }

    if (edgeCount < 0)
    {
        return 0;
    }

    if (edgeCount > 0 &&
        (edgeSources == nullptr ||
         edgeTargets == nullptr ||
         edgeKinds == nullptr ||
         edgeWeights == nullptr))
    {
        return 0;
    }

    const auto safeNodeCount = static_cast<std::size_t>(nodeCount);
    const auto safeEdgeCount = static_cast<std::size_t>(edgeCount);
    return ComputeCoseLayoutCore(
               MakeInputSpan(nodeKinds, safeNodeCount),
               MakeInputSpan(labelLengths, safeNodeCount),
               MakeInputSpan(ownerIndices, safeNodeCount),
               MakeInputSpan(edgeSources, safeEdgeCount),
               MakeInputSpan(edgeTargets, safeEdgeCount),
               MakeInputSpan(edgeKinds, safeEdgeCount),
               MakeInputSpan(edgeWeights, safeEdgeCount),
               MakeOutputSpan(outX, safeNodeCount),
               MakeOutputSpan(outY, safeNodeCount))
               ? 1
               : 0;
}
