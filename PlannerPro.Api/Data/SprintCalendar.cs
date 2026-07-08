using PlannerPro.Api.Domain;

namespace PlannerPro.Api.Data;

/// <summary>Generates the rolling biweekly sprint calendar.
/// Sprint 1 anchors to Mon 2026-07-13; each sprint is a two-week span shown as
/// Monday → second Friday (e.g. 07-13 → 07-24). Starts advance by 14 days.</summary>
public static class SprintCalendar
{
    /// <summary>The anchor start of Sprint 1.</summary>
    public static readonly DateOnly FirstSprintStart = new(2026, 7, 13);

    /// <summary>Generate sprints numbered from <paramref name="startNumber"/>,
    /// beginning at <paramref name="firstStart"/>, for every sprint whose start
    /// date is on or before <paramref name="throughStartOnOrBefore"/>.</summary>
    public static IEnumerable<Sprint> Generate(
        DateOnly firstStart,
        DateOnly throughStartOnOrBefore,
        int startNumber = 1)
    {
        var number = startNumber;
        var start = firstStart;
        while (start <= throughStartOnOrBefore)
        {
            yield return new Sprint
            {
                Number = number,
                StartDate = start,
                EndDate = start.AddDays(11), // Monday of week 1 → Friday of week 2
            };
            number++;
            start = start.AddDays(14);
        }
    }

    /// <summary>Default generation window: Sprint 1 through the last sprint that
    /// starts on or before 2027-12-31 (extends past end of 2027 as requested).</summary>
    public static IEnumerable<Sprint> GenerateThroughEndOf2027() =>
        Generate(FirstSprintStart, new DateOnly(2027, 12, 31));
}
