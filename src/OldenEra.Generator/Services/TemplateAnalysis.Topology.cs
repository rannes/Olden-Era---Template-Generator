using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services;

/// <summary>
/// T-705 — Topology graph stats. Read-only graph analytics derived from the
/// already-emitted <see cref="Variant.Connections"/> on a generated
/// <see cref="RmgTemplate"/>. The generator built a topology graph during
/// generation; this code does NOT recompute that — it inspects the connection
/// list emitted onto the template, treats it as an undirected simple graph
/// (parallel edges deduped, self-loops dropped), and reports:
/// <list type="bullet">
///   <item><description>Average degree across nodes</description></item>
///   <item><description>Diameter (longest shortest path) of the largest connected component</description></item>
///   <item><description>Articulation points (cut vertices / chokepoints) via Tarjan's algorithm</description></item>
/// </list>
/// </summary>
/// <remarks>
/// One report row per variant. Shipped templates ship a single variant, so
/// callers normally see one row. If the template emits multiple variants,
/// each gets its own row so the panel can show them side by side.
/// </remarks>
public static partial class TemplateAnalysis
{
    /// <summary>
    /// Per-variant topology snapshot. <see cref="Diameter"/> is null when the
    /// variant has fewer than two reachable nodes (degenerate). When the graph
    /// is disconnected, diameter is computed over the largest component and
    /// <see cref="ComponentCount"/> reflects the split.
    /// </summary>
    public sealed record TopologyStats(
        string VariantLabel,
        int NodeCount,
        int EdgeCount,
        double AverageDegree,
        int? Diameter,
        int ComponentCount,
        IReadOnlyList<string> ArticulationPoints);

    /// <summary>
    /// Result of <see cref="ComputeTopology"/>. <see cref="Variants"/> is empty
    /// when the template is null or carries no variants/zones — callers should
    /// hide the panel in that case.
    /// </summary>
    public sealed record TopologyReport(IReadOnlyList<TopologyStats> Variants)
    {
        public bool HasData => Variants.Count > 0;
    }

    /// <summary>
    /// Build per-variant topology stats from the emitted connection list.
    /// Returns an empty report for null/empty templates.
    /// </summary>
    public static TopologyReport ComputeTopology(RmgTemplate? template)
    {
        if (template?.Variants is null || template.Variants.Count == 0)
            return new TopologyReport(System.Array.Empty<TopologyStats>());

        var rows = new List<TopologyStats>(template.Variants.Count);
        for (int vi = 0; vi < template.Variants.Count; vi++)
        {
            var variant = template.Variants[vi];
            string label = template.Variants.Count == 1
                ? "Topology"
                : $"Variant {vi + 1}";

            rows.Add(BuildVariantStats(label, variant));
        }
        return new TopologyReport(rows);
    }

    private static TopologyStats BuildVariantStats(string label, Variant variant)
    {
        // Nodes — every named zone in the variant. Unnamed/empty zones are skipped:
        // the connection list addresses zones by name, so a missing name has no edges.
        var nodes = new List<string>();
        var nodeIndex = new Dictionary<string, int>(System.StringComparer.Ordinal);
        if (variant.Zones is not null)
        {
            foreach (var z in variant.Zones)
            {
                if (string.IsNullOrEmpty(z.Name)) continue;
                if (nodeIndex.ContainsKey(z.Name)) continue; // dedupe duplicate names
                nodeIndex[z.Name] = nodes.Count;
                nodes.Add(z.Name);
            }
        }

        // Some emitted templates name endpoints in connections that aren't in
        // the zones list (e.g. stub teleport pads). Add those as implicit nodes
        // so degree accounting matches the visible graph.
        if (variant.Connections is not null)
        {
            foreach (var c in variant.Connections)
            {
                EnsureNode(c.From, nodes, nodeIndex);
                EnsureNode(c.To, nodes, nodeIndex);
            }
        }

        int n = nodes.Count;
        var adj = new HashSet<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new HashSet<int>();

        if (variant.Connections is not null)
        {
            foreach (var c in variant.Connections)
            {
                if (string.IsNullOrEmpty(c.From) || string.IsNullOrEmpty(c.To)) continue;
                if (!nodeIndex.TryGetValue(c.From, out int a)) continue;
                if (!nodeIndex.TryGetValue(c.To, out int b)) continue;
                if (a == b) continue; // drop self-loops
                adj[a].Add(b);
                adj[b].Add(a);
            }
        }

        int edgeCount = 0;
        for (int i = 0; i < n; i++) edgeCount += adj[i].Count;
        edgeCount /= 2;

        double avgDegree = n == 0 ? 0.0 : (2.0 * edgeCount) / n;

        var (diameter, componentCount) = ComputeDiameterAndComponents(adj);
        var cuts = FindArticulationPoints(adj)
            .Select(idx => nodes[idx])
            .OrderBy(name => name, System.StringComparer.Ordinal)
            .ToList();

        return new TopologyStats(
            VariantLabel: label,
            NodeCount: n,
            EdgeCount: edgeCount,
            AverageDegree: avgDegree,
            Diameter: diameter,
            ComponentCount: componentCount,
            ArticulationPoints: cuts);
    }

    private static void EnsureNode(string? name, List<string> nodes, Dictionary<string, int> index)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (index.ContainsKey(name)) return;
        index[name] = nodes.Count;
        nodes.Add(name);
    }

    /// <summary>
    /// BFS each component, take per-node eccentricity, return the maximum across
    /// the largest component (by node count) plus the total component count.
    /// Returns <c>null</c> diameter when fewer than two nodes are reachable.
    /// </summary>
    private static (int? Diameter, int Components) ComputeDiameterAndComponents(HashSet<int>[] adj)
    {
        int n = adj.Length;
        if (n == 0) return (null, 0);

        var componentId = new int[n];
        for (int i = 0; i < n; i++) componentId[i] = -1;

        int components = 0;
        var componentSizes = new List<int>();
        var componentMembers = new List<List<int>>();

        for (int s = 0; s < n; s++)
        {
            if (componentId[s] != -1) continue;
            var members = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(s);
            componentId[s] = components;
            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                members.Add(v);
                foreach (var w in adj[v])
                {
                    if (componentId[w] != -1) continue;
                    componentId[w] = components;
                    queue.Enqueue(w);
                }
            }
            componentSizes.Add(members.Count);
            componentMembers.Add(members);
            components++;
        }

        // Largest component
        int largest = 0;
        for (int i = 1; i < componentSizes.Count; i++)
            if (componentSizes[i] > componentSizes[largest]) largest = i;

        if (componentSizes[largest] < 2) return (null, components);

        // Diameter of largest component: max over all-pairs BFS eccentricity.
        // O(V * (V+E)). Variant graphs ship with <= ~30 zones, so this is fine.
        int diameter = 0;
        var dist = new int[n];
        foreach (var src in componentMembers[largest])
        {
            for (int i = 0; i < n; i++) dist[i] = -1;
            dist[src] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(src);
            int maxFromSrc = 0;
            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                foreach (var w in adj[v])
                {
                    if (dist[w] != -1) continue;
                    dist[w] = dist[v] + 1;
                    if (dist[w] > maxFromSrc) maxFromSrc = dist[w];
                    queue.Enqueue(w);
                }
            }
            if (maxFromSrc > diameter) diameter = maxFromSrc;
        }

        return (diameter, components);
    }

    /// <summary>
    /// Tarjan's articulation-point algorithm on the undirected graph. A node
    /// is an articulation point if removing it (and its incident edges) splits
    /// its connected component into two or more pieces.
    /// </summary>
    private static List<int> FindArticulationPoints(HashSet<int>[] adj)
    {
        int n = adj.Length;
        var result = new List<int>();
        if (n == 0) return result;

        var disc = new int[n];
        var low = new int[n];
        var parent = new int[n];
        var visited = new bool[n];
        var isArt = new bool[n];
        for (int i = 0; i < n; i++) { disc[i] = -1; parent[i] = -1; }

        int timer = 0;
        for (int start = 0; start < n; start++)
        {
            if (visited[start]) continue;
            // Iterative DFS to keep the stack safe even on long chains.
            var stack = new Stack<(int Node, IEnumerator<int> Iter)>();
            visited[start] = true;
            disc[start] = low[start] = timer++;
            stack.Push((start, adj[start].GetEnumerator()));
            int rootChildren = 0;

            while (stack.Count > 0)
            {
                var (u, iter) = stack.Peek();
                if (iter.MoveNext())
                {
                    int v = iter.Current;
                    if (!visited[v])
                    {
                        visited[v] = true;
                        parent[v] = u;
                        disc[v] = low[v] = timer++;
                        if (u == start) rootChildren++;
                        stack.Push((v, adj[v].GetEnumerator()));
                    }
                    else if (v != parent[u])
                    {
                        if (disc[v] < low[u]) low[u] = disc[v];
                    }
                }
                else
                {
                    stack.Pop();
                    if (stack.Count > 0)
                    {
                        var pframe = stack.Peek();
                        int p = pframe.Node;
                        if (low[u] < low[p]) low[p] = low[u];
                        if (p != start && low[u] >= disc[p]) isArt[p] = true;
                    }
                }
            }

            if (rootChildren > 1) isArt[start] = true;
        }

        for (int i = 0; i < n; i++)
            if (isArt[i]) result.Add(i);
        return result;
    }
}
