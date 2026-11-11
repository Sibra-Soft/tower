namespace Tower.Core;

public static class Routing
{
    // Taxi routing deliberately excludes runway edges; entering a runway requires a clearance.
    public static List<Position> FindTaxiPath(Airport airport, string from, string to)
    {
        _ = airport.Point(from);
        _ = airport.Point(to);
        var adjacency = new Dictionary<string, List<(string Node, double Distance)>>(StringComparer.OrdinalIgnoreCase);
        void Connect(string start, string end, double distance)
        {
            if (!adjacency.TryGetValue(start, out var edges))
            {
                edges = [];
                adjacency.Add(start, edges);
            }
            edges.Add((end, distance));
        }
        foreach (var edge in airport.Segments.Where(s => !s.Kind.Equals("RUNWAY", StringComparison.OrdinalIgnoreCase) && !s.Kind.Equals("VROUTE", StringComparison.OrdinalIgnoreCase)))
        {
            var distance = airport.Point(edge.From).Distance(airport.Point(edge.To));
            Connect(edge.From, edge.To, distance);
            if (!edge.OneWay)
            {
                Connect(edge.To, edge.From, distance);
            }
        }
        var distances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { [from] = 0 };
        var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(from, 0);
        while (queue.TryDequeue(out var current, out var priority))
        {
            if (priority > distances[current])
            {
                continue;
            }
            if (current.Equals(to, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (!adjacency.TryGetValue(current, out var edges))
            {
                continue;
            }
            foreach (var edge in edges)
            {
                var candidate = priority + edge.Distance;
                if (distances.TryGetValue(edge.Node, out var known) && known <= candidate)
                {
                    continue;
                }
                distances[edge.Node] = candidate;
                previous[edge.Node] = current;
                queue.Enqueue(edge.Node, candidate);
            }
        }
        if (!distances.ContainsKey(to))
        {
            throw new InvalidDataException($"No connected taxi route from {from} to {to}.");
        }
        var ids = new List<string> { to };
        while (!ids[^1].Equals(from, StringComparison.OrdinalIgnoreCase))
        {
            ids.Add(previous[ids[^1]]);
        }
        ids.Reverse();
        return ids.Select(airport.Point).ToList();
    }
    public static List<Position> NamedRoute(Airport airport, string chain)
    {
        if (airport.Routes.TryGetValue(chain.Trim(),out var exact)) return exact.Select(airport.Point).ToList();
        var names = chain.ToUpperInvariant().Split(['-',',',' '],StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        foreach (var name in names)
        {
            if (!airport.Routes.TryGetValue(name,out var source) || source.Count < 2) throw new InvalidDataException($"Unknown or empty taxi route: {name}");
            var ids = source.ToList();
            if (result.Count == 0) { result.AddRange(ids); continue; }
            if (result[^1].Equals(ids[^1],StringComparison.OrdinalIgnoreCase))
            { if(airport.OneWayRoutes.Contains(name)) throw new InvalidDataException("Cannot reverse a one-way route."); ids.Reverse(); }
            var join = ids.FindIndex(x => x.Equals(result[^1],StringComparison.OrdinalIgnoreCase));
            if (join < 0) throw new InvalidDataException($"Route {name} does not connect to the preceding route.");
            result.AddRange(ids.Skip(join + 1));
        }
        if (result.Count < 2) throw new InvalidDataException("A route needs at least two points.");
        return result.Select(airport.Point).ToList();
    }
}
