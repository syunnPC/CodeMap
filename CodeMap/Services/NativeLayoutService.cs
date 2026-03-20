using CodeMap.Graph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CodeMap.Services;

internal static partial class NativeLayoutService
{
    private static readonly object s_layoutCacheGate = new();
    private static NativeLayoutCacheEntry? s_lastLayoutCacheEntry;

    static NativeLayoutService()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeLayoutService).Assembly, ResolveNativeLibrary);
    }

    private enum NativeNodeKind
    {
        Project = 0,
        Document = 1,
        Symbol = 2,
        Package = 3,
        Assembly = 4,
        Dll = 5
    }

    private enum NativeEdgeKind
    {
        ContainsDocument = 0,
        ContainsSymbol = 1,
        ProjectReference = 2,
        DocumentReference = 3,
        SymbolCall = 4,
        SymbolInheritance = 5,
        SymbolImplementation = 6,
        SymbolCreation = 7,
        SymbolReference = 8,
        ProjectPackage = 9,
        ProjectAssembly = 10,
        ProjectDll = 11,
        Default = 12
    }

    private sealed class NativeLayoutCacheEntry(
        int[] nodeKinds,
        int[] labelLengths,
        int[] ownerIndices,
        int[] edgeSources,
        int[] edgeTargets,
        int[] edgeKinds,
        int[] edgeWeights,
        float[] x,
        float[] y)
    {
        public int[] NodeKinds { get; } = nodeKinds;
        public int[] LabelLengths { get; } = labelLengths;
        public int[] OwnerIndices { get; } = ownerIndices;
        public int[] EdgeSources { get; } = edgeSources;
        public int[] EdgeTargets { get; } = edgeTargets;
        public int[] EdgeKinds { get; } = edgeKinds;
        public int[] EdgeWeights { get; } = edgeWeights;
        public float[] X { get; } = x;
        public float[] Y { get; } = y;
    }

    [LibraryImport("LayoutLib", EntryPoint = "ComputeCoseLayout", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.I4)]
    private static partial int ComputeCoseLayout(
        int nodeCount,
        int[] nodeKinds,
        int[] labelLengths,
        int[] ownerIndices,
        int edgeCount,
        int[] edgeSources,
        int[] edgeTargets,
        int[] edgeKinds,
        int[] edgeWeights,
        float[] outX,
        float[] outY);

    public static bool TryApplyCoseLayout(
        GraphPayload payload,
        out GraphPayload updatedPayload,
        out long elapsedMilliseconds,
        out string failureReason)
    {
        updatedPayload = payload;
        elapsedMilliseconds = 0;
        failureReason = string.Empty;

        if (payload.Nodes.Count == 0)
        {
            return false;
        }

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<string, int> nodeIndexById = new(payload.Nodes.Count, StringComparer.Ordinal);
            int[] nodeKinds = new int[payload.Nodes.Count];
            int[] labelLengths = new int[payload.Nodes.Count];
            int[] ownerIndices = new int[payload.Nodes.Count];
            Array.Fill(ownerIndices, -1);

            for (int nodeIndex = 0; nodeIndex < payload.Nodes.Count; nodeIndex++)
            {
                GraphNodePayload node = payload.Nodes[nodeIndex];
                nodeIndexById[node.Id] = nodeIndex;
                nodeKinds[nodeIndex] = (int)ResolveNodeKind(node.Group);
                labelLengths[nodeIndex] = Math.Clamp(node.Label?.Length ?? 0, 0, 160);
            }

            List<int> edgeSourcesBuilder = new(payload.Edges.Count);
            List<int> edgeTargetsBuilder = new(payload.Edges.Count);
            List<int> edgeKindsBuilder = new(payload.Edges.Count);
            List<int> edgeWeightsBuilder = new(payload.Edges.Count);

            foreach (GraphEdgePayload edge in payload.Edges)
            {
                if (!nodeIndexById.TryGetValue(edge.Source, out int sourceIndex) ||
                    !nodeIndexById.TryGetValue(edge.Target, out int targetIndex))
                {
                    continue;
                }

                switch (edge.Kind)
                {
                    case "contains-document":
                    case "contains-symbol":
                    case "project-package":
                    case "project-assembly":
                    case "project-dll":
                        ownerIndices[targetIndex] = sourceIndex;
                        break;
                }

                edgeSourcesBuilder.Add(sourceIndex);
                edgeTargetsBuilder.Add(targetIndex);
                edgeKindsBuilder.Add((int)ResolveEdgeKind(edge.Kind));
                edgeWeightsBuilder.Add(Math.Max(1, edge.Weight));
            }

            int[] edgeSources = edgeSourcesBuilder.ToArray();
            int[] edgeTargets = edgeTargetsBuilder.ToArray();
            int[] edgeKinds = edgeKindsBuilder.ToArray();
            int[] edgeWeights = edgeWeightsBuilder.ToArray();
            float[] x = new float[payload.Nodes.Count];
            float[] y = new float[payload.Nodes.Count];

            if (TryRestoreCachedLayout(
                nodeKinds,
                labelLengths,
                ownerIndices,
                edgeSources,
                edgeTargets,
                edgeKinds,
                edgeWeights,
                x,
                y))
            {
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            }
            else if (ComputeCoseLayout(
                payload.Nodes.Count,
                nodeKinds,
                labelLengths,
                ownerIndices,
                edgeSources.Length,
                edgeSources,
                edgeTargets,
                edgeKinds,
                edgeWeights,
                x,
                y) == 0)
            {
                failureReason = "native-layout-returned-failure";
                return false;
            }
            else
            {
                CacheLayout(
                    nodeKinds,
                    labelLengths,
                    ownerIndices,
                    edgeSources,
                    edgeTargets,
                    edgeKinds,
                    edgeWeights,
                    x,
                    y);
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            }

            List<GraphNodePayload> updatedNodes = new(payload.Nodes.Count);
            for (int nodeIndex = 0; nodeIndex < payload.Nodes.Count; nodeIndex++)
            {
                GraphNodePayload node = payload.Nodes[nodeIndex];
                updatedNodes.Add(node with
                {
                    X = x[nodeIndex],
                    Y = y[nodeIndex]
                });
            }
            updatedPayload = payload with { Nodes = updatedNodes };
            return true;
        }
        catch (DllNotFoundException ex)
        {
            failureReason = $"dll-not-found:{ex.Message}";
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            failureReason = $"entrypoint-not-found:{ex.Message}";
            return false;
        }
        catch (BadImageFormatException ex)
        {
            failureReason = $"bad-image-format:{ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    private static bool TryRestoreCachedLayout(
        int[] nodeKinds,
        int[] labelLengths,
        int[] ownerIndices,
        int[] edgeSources,
        int[] edgeTargets,
        int[] edgeKinds,
        int[] edgeWeights,
        float[] x,
        float[] y)
    {
        lock (s_layoutCacheGate)
        {
            NativeLayoutCacheEntry? cacheEntry = s_lastLayoutCacheEntry;
            if (cacheEntry is null ||
                !cacheEntry.NodeKinds.AsSpan().SequenceEqual(nodeKinds) ||
                !cacheEntry.LabelLengths.AsSpan().SequenceEqual(labelLengths) ||
                !cacheEntry.OwnerIndices.AsSpan().SequenceEqual(ownerIndices) ||
                !cacheEntry.EdgeSources.AsSpan().SequenceEqual(edgeSources) ||
                !cacheEntry.EdgeTargets.AsSpan().SequenceEqual(edgeTargets) ||
                !cacheEntry.EdgeKinds.AsSpan().SequenceEqual(edgeKinds) ||
                !cacheEntry.EdgeWeights.AsSpan().SequenceEqual(edgeWeights))
            {
                return false;
            }

            cacheEntry.X.AsSpan().CopyTo(x);
            cacheEntry.Y.AsSpan().CopyTo(y);
            return true;
        }
    }

    private static void CacheLayout(
        int[] nodeKinds,
        int[] labelLengths,
        int[] ownerIndices,
        int[] edgeSources,
        int[] edgeTargets,
        int[] edgeKinds,
        int[] edgeWeights,
        float[] x,
        float[] y)
    {
        lock (s_layoutCacheGate)
        {
            s_lastLayoutCacheEntry = new NativeLayoutCacheEntry(
                (int[])nodeKinds.Clone(),
                (int[])labelLengths.Clone(),
                (int[])ownerIndices.Clone(),
                (int[])edgeSources.Clone(),
                (int[])edgeTargets.Clone(),
                (int[])edgeKinds.Clone(),
                (int[])edgeWeights.Clone(),
                (float[])x.Clone(),
                (float[])y.Clone());
        }
    }

    private static NativeNodeKind ResolveNodeKind(string group)
    {
        return group switch
        {
            "project" => NativeNodeKind.Project,
            "document" => NativeNodeKind.Document,
            "symbol" => NativeNodeKind.Symbol,
            "package" => NativeNodeKind.Package,
            "assembly" => NativeNodeKind.Assembly,
            "dll" => NativeNodeKind.Dll,
            _ => NativeNodeKind.Document
        };
    }

    private static NativeEdgeKind ResolveEdgeKind(string kind)
    {
        return kind switch
        {
            "contains-document" => NativeEdgeKind.ContainsDocument,
            "contains-symbol" => NativeEdgeKind.ContainsSymbol,
            "project-reference" => NativeEdgeKind.ProjectReference,
            "document-reference" => NativeEdgeKind.DocumentReference,
            "symbol-call" => NativeEdgeKind.SymbolCall,
            "symbol-inheritance" => NativeEdgeKind.SymbolInheritance,
            "symbol-implementation" => NativeEdgeKind.SymbolImplementation,
            "symbol-creation" => NativeEdgeKind.SymbolCreation,
            "symbol-reference" => NativeEdgeKind.SymbolReference,
            "project-package" => NativeEdgeKind.ProjectPackage,
            "project-assembly" => NativeEdgeKind.ProjectAssembly,
            "project-dll" => NativeEdgeKind.ProjectDll,
            _ => NativeEdgeKind.Default
        };
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "LayoutLib", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (string candidatePath in EnumerateCandidatePaths())
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            return NativeLibrary.Load(candidatePath);
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "LayoutLib.dll");

        string architectureDirectory = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "ARM64",
            _ => "x64"
        };

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int depth = 0; current is not null && depth < 8; depth++)
        {
            string debugCandidate = Path.Combine(current.FullName, "Native", architectureDirectory, "Debug", "LayoutLib.dll");
            yield return debugCandidate;

            string releaseCandidate = Path.Combine(current.FullName, "Native", architectureDirectory, "Release", "LayoutLib.dll");
            yield return releaseCandidate;

            current = current.Parent;
        }
    }
}
