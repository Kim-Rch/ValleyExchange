using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ValleyExchange;

/// <summary>Pure tile pathfinding (no game state), so it can be checked outside Stardew.</summary>
public static class PathGrid
{
    private static readonly Point[] Steps = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };

    /// <summary>
    /// A* over walkable tiles. If the goal isn't reached within <paramref name="maxNodes"/> expansions: null, or with
    /// <paramref name="partial"/> the path to the explored tile closest to the goal (so a walker can head that way instead of vanishing).
    /// </summary>
    public static List<Point>? Find(Func<Point, bool> passable, Point start, Point goal, int maxNodes = 4000, bool partial = false)
    {
        var open = new PriorityQueue<Point, int>();
        var cameFrom = new Dictionary<Point, Point>();
        var cost = new Dictionary<Point, int> { [start] = 0 };
        var known = new Dictionary<Point, bool>();
        var best = start;
        int bestDistance = Distance(start, goal);
        open.Enqueue(start, 0);
        while (open.TryDequeue(out var current, out _) && maxNodes-- > 0)
        {
            if (current == goal)
                return Trace(cameFrom, start, goal);
            int distance = Distance(current, goal);
            if (distance < bestDistance)
                (best, bestDistance) = (current, distance);
            foreach (var step in Steps)
            {
                var next = new Point(current.X + step.X, current.Y + step.Y);
                if (!known.TryGetValue(next, out bool ok))
                    known[next] = ok = passable(next);
                int nextCost = cost[current] + 1;
                if (!ok || (cost.TryGetValue(next, out int seen) && seen <= nextCost))
                    continue;
                cost[next] = nextCost;
                cameFrom[next] = current;
                open.Enqueue(next, nextCost + Distance(next, goal));
            }
        }
        return partial ? Trace(cameFrom, start, best) : null;
    }

    private static int Distance(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static List<Point> Trace(Dictionary<Point, Point> cameFrom, Point start, Point end)
    {
        var path = new List<Point>();
        for (var p = end; p != start; p = cameFrom[p])
            path.Add(p);
        path.Reverse();
        return path;
    }
}
