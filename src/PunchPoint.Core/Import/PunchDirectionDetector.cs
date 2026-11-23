using PunchPoint.Core.Models;

namespace PunchPoint.Core.Import;

public sealed class DirectionDetection
{
    public required string Description { get; init; }
    /// <summary>0-100. 100 means the stored codes perfectly describe IN/OUT.</summary>
    public int Confidence { get; init; }
    public PunchDirectionMode Mode { get; init; } = PunchDirectionMode.Alternate;
    public string Column { get; init; } = "";
    public int InValue { get; init; }
    public int OutValue { get; init; }
    public bool ZkStyle { get; init; }
}

/// <summary>
/// Works out how the legacy software encoded IN versus OUT.
///
/// timeQplus stores every terminal event in one Transactions table. Depending on the
/// version, the direction lives in TransState, TransType or is not stored at all.
/// Rather than guessing from a single sample, this scores each candidate column by how
/// well it alternates per employee, and falls back to alternation (which is exactly how
/// a two-punch-per-day record behaves).
/// </summary>
public static class PunchDirectionDetector
{
    private static readonly PunchKind[] ZkKinds =
    {
        PunchKind.In,        // 0 Check-In
        PunchKind.Out,       // 1 Check-Out
        PunchKind.BreakOut,  // 2 Break-Out
        PunchKind.BreakIn,   // 3 Break-In
        PunchKind.In,        // 4 OT-In
        PunchKind.Out,       // 5 OT-Out
    };

    public static DirectionDetection Detect(IReadOnlyList<LegacyTransaction> transactions, PunchDirectionMode mode)
    {
        var punches = transactions.Where(t => !IsMiscHours(t)).OrderBy(t => t.Timestamp).ToList();

        if (mode == PunchDirectionMode.Alternate || punches.Count == 0)
        {
            return new DirectionDetection
            {
                Description = punches.Count == 0
                    ? "No punches to analyse"
                    : "Punches alternated IN → OUT (no usable direction codes)",
                Mode = PunchDirectionMode.Alternate,
                Confidence = punches.Count == 0 ? 0 : 60,
            };
        }

        if (mode == PunchDirectionMode.UseState)
        {
            return new DirectionDetection
            {
                Description = "Using TransState (0 = IN, 1 = OUT) as requested",
                Mode = PunchDirectionMode.UseState,
                Column = "State",
                InValue = 0,
                OutValue = 1,
                Confidence = 100,
            };
        }

        var sequences = punches
            .GroupBy(t => t.Badge, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(t => t.Timestamp).ToList())
            .Where(s => s.Count > 1)
            .ToList();

        if (sequences.Count == 0)
        {
            return new DirectionDetection
            {
                Description = "Punches alternated IN → OUT (not enough history to detect codes)",
                Mode = PunchDirectionMode.Alternate,
                Confidence = 60,
            };
        }

        var candidates = new List<DirectionDetection>();

        // Binary candidate columns.
        foreach (var (name, selector) in new (string, Func<LegacyTransaction, int>)[]
                 {
                     ("State", t => t.State),
                     ("Type", t => t.Type),
                     ("Source", t => t.Source),
                 })
        {
            var values = punches.Select(selector).Distinct().OrderBy(v => v).ToList();
            if (values.Count != 2) continue;

            double score = FlipScore(sequences, selector);
            candidates.Add(new DirectionDetection
            {
                Description = $"{name} column ({values[0]} = IN, {values[1]} = OUT), {Pct(score)} consistent",
                Mode = PunchDirectionMode.UseState,
                Column = name,
                InValue = values[0],
                OutValue = values[1],
                Confidence = Pct(score),
            });
        }

        // ZKTeco-style state codes 0..5 (the terminal SDK timeQplus is built on).
        var stateValues = punches.Select(t => t.State).Distinct().ToList();
        if (stateValues.Count >= 2 && stateValues.Count <= 6 && stateValues.All(v => v is >= 0 and <= 5))
        {
            double score = FlipScore(sequences, t => ZkKinds[Math.Clamp(t.State, 0, 5)] is PunchKind.In or PunchKind.BreakIn ? 0 : 1);
            candidates.Add(new DirectionDetection
            {
                Description = $"Terminal state codes 0-5 (Check-In = 0, Check-Out = 1), {Pct(score)} consistent",
                Mode = PunchDirectionMode.UseState,
                Column = "State",
                InValue = 0,
                OutValue = 1,
                ZkStyle = true,
                Confidence = Pct(score),
            });
        }

        var best = candidates.OrderByDescending(c => c.Confidence).FirstOrDefault();

        // A column only wins if it actually behaves like an alternating IN/OUT stream.
        if (best is not null && best.Confidence >= 85)
            return best;

        string why = best is null
            ? "no direction column found"
            : $"best candidate ({best.Column}) only {best.Confidence}% consistent";

        return new DirectionDetection
        {
            Description = $"Punches alternated IN → OUT ({why})",
            Mode = PunchDirectionMode.Alternate,
            Confidence = 60,
        };
    }

    public static void Assign(List<LegacyTransaction> transactions, DirectionDetection detection)
    {
        var punches = transactions.Where(t => !IsMiscHours(t)).OrderBy(t => t.Timestamp).ToList();

        if (detection.Mode == PunchDirectionMode.UseState && detection.ZkStyle)
        {
            foreach (var t in punches)
                t.Kind = ZkKinds[Math.Clamp(t.State, 0, 5)];
        }
        else if (detection.Mode == PunchDirectionMode.UseState)
        {
            foreach (var t in punches)
            {
                int value = detection.Column switch
                {
                    "State" => t.State,
                    "Type" => t.Type,
                    "Source" => t.Source,
                    _ => t.State,
                };
                t.Kind = value == detection.InValue ? PunchKind.In : PunchKind.Out;
            }
        }
        else
        {
            AssignAlternating(punches);
        }

        foreach (var misc in transactions.Where(IsMiscHours))
            misc.Kind = PunchKind.Hours;
    }

    /// <summary>
    /// Alternates IN/OUT per employee. If a punch arrives more than 14 hours after an
    /// unmatched IN, the IN is treated as a missed OUT and the sequence restarts with IN.
    /// </summary>
    private static void AssignAlternating(List<LegacyTransaction> punches)
    {
        foreach (var group in punches.GroupBy(t => t.Badge, StringComparer.OrdinalIgnoreCase))
        {
            bool expectingIn = true;
            DateTime? openIn = null;

            foreach (var t in group.OrderBy(x => x.Timestamp))
            {
                if (expectingIn)
                {
                    t.Kind = PunchKind.In;
                    expectingIn = false;
                    openIn = t.Timestamp;
                }
                else
                {
                    bool stale = openIn is not null && (t.Timestamp - openIn.Value).TotalHours > 14;
                    if (stale)
                    {
                        // Previous IN never got an OUT: start a new session.
                        t.Kind = PunchKind.In;
                        openIn = t.Timestamp;
                    }
                    else
                    {
                        t.Kind = PunchKind.Out;
                        expectingIn = true;
                        openIn = null;
                    }
                }
            }
        }
    }

    internal static bool IsMiscHours(LegacyTransaction t) => t.Hours > 0;

    private static double FlipScore(IEnumerable<List<LegacyTransaction>> sequences, Func<LegacyTransaction, int> selector)
    {
        int transitions = 0;
        int flips = 0;

        foreach (var sequence in sequences)
        {
            for (int i = 1; i < sequence.Count; i++)
            {
                if ((sequence[i].Timestamp - sequence[i - 1].Timestamp).TotalHours > 14)
                {
                    // A new session: the first punch after a long gap should be an IN,
                    // but it does not tell us anything about the column encoding.
                    continue;
                }
                transitions++;
                if (selector(sequence[i]) != selector(sequence[i - 1]))
                    flips++;
            }
        }

        return transitions == 0 ? 0 : flips / (double)transitions;
    }

    private static int Pct(double fraction) => (int)Math.Round(fraction * 100);
}
