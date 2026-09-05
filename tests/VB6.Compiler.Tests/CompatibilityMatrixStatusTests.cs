using System.Text.RegularExpressions;

namespace VB6.Compiler.Tests;

/// <summary>
/// The status rules of the compatibility matrix, mechanised.
///
/// <c>CLAUDE.md</c> states four rules that were, until R0, checked only by reading: dependencies
/// resolve and stay acyclic, a card never waits on a later milestone, an open card carries a
/// milestone and appears in the roadmap, and an area status is derived from the expectations below
/// it rather than written by hand. Each held when it was checked by hand -- which is precisely why
/// they need a machine: a rule that is only ever confirmed manually is a rule that drifts on the
/// first day nobody looks.
///
/// The counts and the oracle rule are checked in <see cref="CompatibilityMatrixTests"/>.
/// </summary>
[TestClass]
public sealed class CompatibilityMatrixStatusTests
{
    [TestMethod]
    public void Matrix_ResolvesEveryDependency()
    {
        var expectations = CompatibilityMatrix.LoadExpectations();
        var known = expectations.Select(expectation => expectation.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = expectations
            .SelectMany(expectation => expectation.DependsOn.Select(dependency => (expectation.Id, dependency)))
            .Where(pair => !known.Contains(pair.dependency))
            .Select(pair => pair.Id + " -> " + pair.dependency)
            .ToArray();

        Assert.AreEqual(
            0,
            unknown.Length,
            "dependsOn nennt unbekannte Karten: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void Matrix_HasNoDependencyCycle()
    {
        var expectations = CompatibilityMatrix.LoadExpectations();
        var byId = expectations.ToDictionary(expectation => expectation.Id, StringComparer.OrdinalIgnoreCase);

        // Ein Zyklus macht die Reihenfolge unausführbar, ohne dass eine einzelne Karte falsch
        // aussieht. Deshalb wird der gefundene Ring benannt und nicht nur gezählt.
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var path = new Stack<string>();
        var cycles = new List<string>();

        foreach (var expectation in expectations)
        {
            Visit(expectation.Id);
        }

        Assert.AreEqual(0, cycles.Count, "Zyklische Abhängigkeit: " + string.Join(" | ", cycles));

        void Visit(string id)
        {
            if (state.TryGetValue(id, out var visited))
            {
                if (visited == 1)
                {
                    cycles.Add(string.Join(" -> ", path.Reverse().SkipWhile(step => !string.Equals(step, id, StringComparison.OrdinalIgnoreCase))) + " -> " + id);
                }

                return;
            }

            state[id] = 1;
            path.Push(id);
            foreach (var dependency in byId[id].DependsOn.Where(byId.ContainsKey))
            {
                Visit(dependency);
            }

            path.Pop();
            state[id] = 2;
        }
    }

    [TestMethod]
    public void Matrix_KeepsDependenciesAtOrBeforeTheirOwnMilestone()
    {
        var expectations = CompatibilityMatrix.LoadExpectations();
        var byId = expectations.ToDictionary(expectation => expectation.Id, StringComparer.OrdinalIgnoreCase);

        // R0 vor R1 vor R2: eine Karte, die auf eine spätere Etappe wartet, kann in ihrer eigenen
        // Etappe nicht geschlossen werden. Das ist kein Stilfehler, sondern eine Reihenfolge, die
        // niemand ausführen kann -- und sie fällt beim Lesen nicht auf.
        var violations = expectations
            .Where(expectation => expectation.Milestone is not null)
            .SelectMany(expectation => expectation.DependsOn
                .Where(dependency => byId.TryGetValue(dependency, out var target)
                    && MilestoneRank(target.Milestone) > MilestoneRank(expectation.Milestone))
                .Select(dependency => $"{expectation.Id} ({expectation.Milestone}) -> {dependency} ({byId[dependency].Milestone})"))
            .ToArray();

        Assert.AreEqual(
            0,
            violations.Length,
            "Karte hängt an einer späteren Etappe: " + string.Join(", ", violations));

        static int MilestoneRank(string? milestone) =>
            milestone is { Length: 2 } && milestone[0] == 'R' && char.IsAsciiDigit(milestone[1])
                ? milestone[1] - '0'
                : -1;
    }

    [TestMethod]
    public void Matrix_GivesEveryOpenCardAMilestoneAndARoadmapEntry()
    {
        var expectations = CompatibilityMatrix.LoadExpectations();
        var roadmap = File.ReadAllText(
            Path.Combine(CompatibilityMatrix.FindRepositoryRoot(), "docs", "ROADMAP.md"));

        var withoutMilestone = expectations
            .Where(expectation => expectation.Implementation == "planned")
            .Where(expectation => expectation.Milestone is null || !Regex.IsMatch(expectation.Milestone, "^R[0-7]$"))
            .Select(expectation => expectation.Id)
            .ToArray();
        Assert.AreEqual(
            0,
            withoutMilestone.Length,
            "Offene Karte ohne Etappe R0-R7: " + string.Join(", ", withoutMilestone));

        // Eine geschlossene Karte trägt keine Etappe mehr. Sonst wächst neben der Roadmap eine
        // zweite Restliste heran, und genau die soll die Zuordnungstabelle ersetzen.
        var closedWithMilestone = expectations
            .Where(expectation => expectation.Implementation != "planned" && expectation.Milestone is not null)
            .Select(expectation => expectation.Id)
            .ToArray();
        Assert.AreEqual(
            0,
            closedWithMilestone.Length,
            "Geschlossene Karte trägt noch eine Etappe: " + string.Join(", ", closedWithMilestone));

        var missingFromRoadmap = expectations
            .Where(expectation => expectation.Implementation == "planned")
            .Where(expectation => !roadmap.Contains(expectation.Id, StringComparison.Ordinal))
            .Select(expectation => expectation.Id)
            .ToArray();
        Assert.AreEqual(
            0,
            missingFromRoadmap.Length,
            "Offene Karte fehlt in ROADMAP.md: " + string.Join(", ", missingFromRoadmap));
    }

    [TestMethod]
    public void Matrix_DerivesAreaStatusFromItsExpectations()
    {
        var expectations = CompatibilityMatrix.LoadExpectations();
        var mismatches = new List<string>();

        foreach (var area in CompatibilityMatrix.LoadAreas())
        {
            var children = expectations
                .Where(expectation => string.Equals(expectation.MatrixEntry, area.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (children.Length == 0)
            {
                mismatches.Add(area.Id + ": kein einziger Vertrag zugeordnet");
                continue;
            }

            // Alle umgesetzt = implemented, alle geplant = planned, sonst partial.
            var implementations = children.Select(child => child.Implementation).Distinct(StringComparer.Ordinal).ToArray();
            var derivedImplementation = implementations.Length == 1 ? implementations[0] : "partial";
            if (area.Implementation != derivedImplementation)
            {
                mismatches.Add($"{area.Id}: implementation ist '{area.Implementation}', abgeleitet wäre '{derivedImplementation}'");
            }

            // Ein einziges nicht verifiziertes Kind nimmt dem Bereich die Verifikationszusage.
            var verifications = children.Select(child => child.Verification).Distinct(StringComparer.Ordinal).ToArray();
            var derivedVerification = verifications.Contains("not-yet-verified")
                ? "not-yet-verified"
                : verifications.Length == 1
                    ? verifications[0]
                    : "documented-verified";
            if (area.Verification != derivedVerification)
            {
                mismatches.Add($"{area.Id}: verification ist '{area.Verification}', abgeleitet wäre '{derivedVerification}'");
            }

            // Ein offener Bereich muss sagen, welche Karte ihn offen hält. "Teilweise" allein ist
            // keine Auskunft, an der jemand weiterarbeiten kann.
            var open = children.Where(child => child.Implementation != "implemented").Select(child => child.Id).ToArray();
            if (open.Length > 0 && string.IsNullOrWhiteSpace(area.Gap))
            {
                mismatches.Add($"{area.Id}: offen ({string.Join(", ", open)}), nennt aber keinen gap");
            }
            else if (open.Length > 0 && !open.Any(card => area.Gap!.Contains(card, StringComparison.OrdinalIgnoreCase)))
            {
                mismatches.Add($"{area.Id}: gap nennt keine der offenen Karten ({string.Join(", ", open)})");
            }
            else if (open.Length == 0 && !string.IsNullOrWhiteSpace(area.Gap))
            {
                mismatches.Add($"{area.Id}: vollständig umgesetzt, führt aber noch einen gap");
            }
        }

        Assert.AreEqual(0, mismatches.Count, string.Join(Environment.NewLine, mismatches));
    }
}
