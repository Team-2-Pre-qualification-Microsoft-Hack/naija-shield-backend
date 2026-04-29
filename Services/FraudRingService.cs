using naija_shield_backend.Models;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Services;

/// <summary>
/// Detects coordinated fraud rings by building an undirected graph over recent
/// blocked/monitoring incidents and running Union-Find connected-component analysis.
///
/// Two caller numbers receive a graph edge when:
///   1. SharedVictim  — they both appear in incidents targeting the same destination
///                      number (strongest signal — same operation hitting same targets).
///   2. TimeProximity — different callers produce the same classification within a
///                      90-minute window (catches rotating SIMs in a burst campaign).
///
/// Connected components with ≥ minCallers members are surfaced as FraudRings.
/// Ring IDs are deterministic (SHA-256 of sorted caller numbers) so the same ring
/// gets the same ID across successive analysis runs.
/// </summary>
public class FraudRingService
{
    private readonly IIncidentRepository _repository;
    private readonly ILogger<FraudRingService> _logger;

    public FraudRingService(IIncidentRepository repository, ILogger<FraudRingService> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    /// <summary>
    /// Runs the full ring-detection pipeline over incidents from the last
    /// <paramref name="hours"/> hours and returns rings with at least
    /// <paramref name="minCallers"/> distinct caller numbers.
    /// </summary>
    public async Task<List<FraudRing>> DetectRingsAsync(
        int hours      = 72,
        int minCallers = 2,
        CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-hours);
        var all    = await _repository.GetByDateRangeAsync(cutoff, DateTimeOffset.UtcNow, ct);

        // Only blocked/monitoring incidents — "Allowed" incidents are too noisy for ring analysis.
        var relevant = all
            .Where(i => i.Status is "Blocked" or "Monitoring")
            .ToList();

        _logger.LogInformation(
            "[FraudRing] {Total} total incidents, {Relevant} relevant in last {Hours}h",
            all.Count, relevant.Count, hours);

        if (relevant.Count < 2)
            return [];

        var uf = new UnionFind();

        // Seed every caller into the structure
        foreach (var inc in relevant)
            uf.Add(inc.From);

        bool anySharedVictim    = false;
        bool anyTimeProximity   = false;

        // ── Edge type 1: SharedVictim ─────────────────────────────────────────
        // Group by destination number; if ≥2 distinct callers hit the same victim → edge.
        var byVictim = relevant
            .Where(i => !string.IsNullOrEmpty(i.To))
            .GroupBy(i => i.To!)
            .Where(g => g.Select(i => i.From).Distinct().Count() >= 2);

        foreach (var group in byVictim)
        {
            var callers = group.Select(i => i.From).Distinct().ToList();
            for (int i = 1; i < callers.Count; i++)
                uf.Union(callers[0], callers[i]);
            anySharedVictim = true;
        }

        // ── Edge type 2: TimeProximity ────────────────────────────────────────
        // Within each classification, sort by timestamp; adjacent incidents from
        // DIFFERENT callers within 90 minutes get an edge.
        const double windowMinutes = 90;
        var byClass = relevant.GroupBy(i => i.Classification);

        foreach (var classGroup in byClass)
        {
            var sorted = classGroup
                .OrderBy(i => i.Timestamp)
                .ToList();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var a = sorted[i];
                var b = sorted[i + 1];
                if (a.From == b.From) continue;

                var ta = DateTimeOffset.TryParse(a.Timestamp, out var dta) ? dta : DateTimeOffset.MinValue;
                var tb = DateTimeOffset.TryParse(b.Timestamp, out var dtb) ? dtb : DateTimeOffset.MinValue;

                if (Math.Abs((tb - ta).TotalMinutes) <= windowMinutes)
                {
                    uf.Union(a.From, b.From);
                    anyTimeProximity = true;
                }
            }
        }

        // ── Build ring objects from connected components ───────────────────────
        var components = uf.GetComponents();
        var rings      = new List<FraudRing>();

        foreach (var (_, members) in components)
        {
            if (members.Count < minCallers) continue;

            var ringIncidents = relevant
                .Where(i => members.Contains(i.From))
                .ToList();

            var ring = BuildRing(members, ringIncidents, anySharedVictim, anyTimeProximity);
            rings.Add(ring);

            _logger.LogInformation(
                "[FraudRing] Ring {Id} — {Callers} callers, {Victims} victims, {Incidents} incidents, score={Score}",
                ring.RingId, ring.CallerNumbers.Count, ring.VictimNumbers.Count,
                ring.TotalIncidents, ring.RingSeverityScore);
        }

        return [.. rings.OrderByDescending(r => r.RingSeverityScore)];
    }

    /// <summary>
    /// Returns the full detail for one ring (same analysis window), including all incidents.
    /// Returns null if no ring with that ID is found in the current window.
    /// </summary>
    public async Task<FraudRingDetail?> GetRingDetailAsync(
        string ringId,
        int hours = 72,
        CancellationToken ct = default)
    {
        var rings = await DetectRingsAsync(hours, minCallers: 2, ct);
        var ring  = rings.FirstOrDefault(r => r.RingId == ringId);
        if (ring is null) return null;

        // Re-fetch incidents for the ring members only
        var cutoff = DateTimeOffset.UtcNow.AddHours(-hours);
        var all    = await _repository.GetByDateRangeAsync(cutoff, DateTimeOffset.UtcNow, ct);
        var callerSet = ring.CallerNumbers.ToHashSet();

        var incidents = all
            .Where(i => callerSet.Contains(i.From) && i.Status is "Blocked" or "Monitoring")
            .OrderByDescending(i => i.Timestamp)
            .ToList();

        return new FraudRingDetail
        {
            RingId                 = ring.RingId,
            CallerNumbers          = ring.CallerNumbers,
            VictimNumbers          = ring.VictimNumbers,
            TotalIncidents         = ring.TotalIncidents,
            DominantClassification = ring.DominantClassification,
            Classifications        = ring.Classifications,
            Channels               = ring.Channels,
            Languages              = ring.Languages,
            States                 = ring.States,
            FirstSeen              = ring.FirstSeen,
            LastSeen               = ring.LastSeen,
            RingSeverityScore      = ring.RingSeverityScore,
            EdgeBasis              = ring.EdgeBasis,
            IsActive               = ring.IsActive,
            Incidents              = incidents
        };
    }

    // ── Ring construction ─────────────────────────────────────────────────────

    private static FraudRing BuildRing(
        List<string>       callers,
        List<ThreatIncident> incidents,
        bool anySharedVictim,
        bool anyTimeProximity)
    {
        var victims = incidents
            .Where(i => !string.IsNullOrEmpty(i.To))
            .Select(i => i.To!)
            .Distinct()
            .ToList();

        // Victims shared between ≥2 callers in THIS ring
        var callerSet = callers.ToHashSet();
        var sharedVictims = incidents
            .Where(i => !string.IsNullOrEmpty(i.To))
            .GroupBy(i => i.To!)
            .Where(g => g.Select(i => i.From).Distinct().Count(f => callerSet.Contains(f)) >= 2)
            .Select(g => g.Key)
            .ToHashSet();

        var hasSharedVictim  = sharedVictims.Count > 0;
        var edgeBasis = hasSharedVictim && anyTimeProximity ? "Mixed"
                      : hasSharedVictim                     ? "SharedVictim"
                      :                                       "TimeProximity";

        var dominantClass = incidents
            .GroupBy(i => i.Classification)
            .OrderByDescending(g => g.Count())
            .First().Key;

        var lastSeen  = incidents.Max(i => i.Timestamp)!;
        var isActive  = DateTimeOffset.TryParse(lastSeen, out var ls) &&
                        (DateTimeOffset.UtcNow - ls).TotalHours <= 24;

        var avgRisk      = incidents.Average(i => i.RiskScore);
        var sizeBoost    = 1.0 + (callers.Count  - 1) * 0.20;  // +20% per extra caller
        var victimBoost  = 1.0 + Math.Min(victims.Count, 10)   * 0.05;  // +5% per victim, cap at 10
        var recencyBoost = isActive ? 1.2 : 1.0;
        var score        = (int)Math.Round(Math.Clamp(avgRisk * sizeBoost * victimBoost * recencyBoost, 0, 100));

        // Deterministic ring ID — same callers always produce the same ID
        var sorted   = string.Join(",", callers.OrderBy(x => x));
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(sorted));
        var ringId   = $"RING-{Convert.ToHexString(hashBytes)[..6].ToUpper()}";

        return new FraudRing
        {
            RingId                 = ringId,
            CallerNumbers          = [.. callers.OrderBy(x => x)],
            VictimNumbers          = victims,
            TotalIncidents         = incidents.Count,
            DominantClassification = dominantClass,
            Classifications        = [.. incidents.Select(i => i.Classification).Distinct()],
            Channels               = [.. incidents.Select(i => i.Channel).Distinct()],
            Languages              = [.. incidents
                                        .Where(i => !string.IsNullOrEmpty(i.DetectedLanguage))
                                        .Select(i => i.DetectedLanguage!)
                                        .Distinct()],
            States                 = [.. incidents
                                        .Where(i => !string.IsNullOrEmpty(i.State))
                                        .Select(i => i.State!)
                                        .Distinct()],
            FirstSeen              = incidents.Min(i => i.Timestamp)!,
            LastSeen               = lastSeen,
            RingSeverityScore      = score,
            EdgeBasis              = edgeBasis,
            IsActive               = isActive
        };
    }

    // ── Union-Find (path compression + union by rank) ─────────────────────────

    private sealed class UnionFind
    {
        private readonly Dictionary<string, string> _parent = [];
        private readonly Dictionary<string, int>    _rank   = [];

        public void Add(string x)
        {
            if (_parent.ContainsKey(x)) return;
            _parent[x] = x;
            _rank[x]   = 0;
        }

        public string Find(string x)
        {
            if (!_parent.ContainsKey(x)) Add(x);
            if (_parent[x] != x)
                _parent[x] = Find(_parent[x]); // path compression
            return _parent[x];
        }

        public void Union(string x, string y)
        {
            Add(x); Add(y);
            var rx = Find(x);
            var ry = Find(y);
            if (rx == ry) return;

            // Union by rank keeps the tree flat
            if (_rank[rx] < _rank[ry]) (rx, ry) = (ry, rx);
            _parent[ry] = rx;
            if (_rank[rx] == _rank[ry]) _rank[rx]++;
        }

        public Dictionary<string, List<string>> GetComponents()
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var key in _parent.Keys)
            {
                var root = Find(key);
                if (!result.TryGetValue(root, out var list))
                    result[root] = list = [];
                list.Add(key);
            }
            return result;
        }
    }
}
