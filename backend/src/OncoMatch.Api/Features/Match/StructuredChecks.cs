using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using OncoMatch.Api.Features.Patients;
using OncoMatch.Api.Features.Studies;

namespace OncoMatch.Api.Features.Match;

public record StructuredFinding(string Result, string Explanation, object PatientEvidence, object StudyEvidence);
public record StructuredCheckResult(StructuredFinding Age, StructuredFinding Sex)
{
    public bool Excludes => Age.Result == "excluded" || Sex.Result == "excluded";
}

public static class StructuredChecks
{
    private record Bound(BigInteger Count, string Unit);
    private static readonly ConcurrentDictionary<int, (BigInteger Low, BigInteger High)> DayEnvelopes = new();
    private static readonly ConcurrentDictionary<(Bound, Bound), bool> ValidIntervals = new();
    private static readonly Regex AgePattern = new(@"\A([0-9]+)\s+(years?|months?|weeks?|days?|hours?|minutes?)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static StructuredCheckResult Assess(PatientDetails patient, StudyDetails study)
    {
        var (ageResult, reason) = Age(patient.AgeYears, study.MinimumAge, study.MaximumAge);
        var requiredSex = study.Sex?.Trim().ToUpperInvariant();
        var patientSex = patient.Sex.ToUpperInvariant();
        var sexResult = requiredSex == "ALL" ? "noRestrictionIdentified"
            : requiredSex is not ("MALE" or "FEMALE") || patientSex is not ("MALE" or "FEMALE") ? "undetermined"
            : requiredSex == patientSex ? "satisfied" : "excluded";
        return new(new(ageResult, reason, new { patient.AgeYears }, new { study.MinimumAge, study.MaximumAge }),
            new(sexResult, requiredSex == "ALL" ? "Structured sex explicitly permits all sexes."
                : sexResult == "undetermined" ? "Missing or unsupported structured sex prevents comparison."
                : "Compared the known patient sex with the structured study sex.", new { patient.Sex }, new { study.Sex }));
    }

    private static Bound? Parse(string? raw)
    {
        var match = AgePattern.Match(raw?.Trim() ?? "");
        if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return null;
        return new(number, match.Groups[2].Value.ToLowerInvariant().TrimEnd('s'));
    }

    private static (string, string) Age(int? years, string? minimum, string? maximum)
    {
        var min = Parse(minimum);
        var max = Parse(maximum);
        if (min is not null && max is not null && !ValidIntervals.GetOrAdd((min, max), key => ValidForEveryAnchor(key.Item1, key.Item2)))
            return ("undetermined", "The recognized age interval is empty or its validity depends on unknown calendar context.");
        if (years is null or < 0) return ("undetermined", "Patient completed years are unknown or invalid.");
        string Compare(Bound? bound, bool lower)
        {
            if (bound is null) return "undetermined";
            var range = Range(years.Value, bound.Unit);
            return lower ? range.Low >= bound.Count ? "satisfied" : range.High < bound.Count ? "excluded" : "undetermined"
                : range.High <= bound.Count ? "satisfied" : range.Low > bound.Count ? "excluded" : "undetermined";
        }
        var results = new[] { Compare(min, true), Compare(max, false) };
        var result = results.Contains("excluded") ? "excluded" : results.All(r => r == "satisfied") ? "satisfied" : "undetermined";
        return (result, result == "undetermined" ? "Missing/unsupported bounds or whole-year precision prevent a reliable comparison."
            : "Compared inclusive age bounds with the possible completed ages over the Gregorian calendar cycle.");
    }

    private static (BigInteger Low, BigInteger High) Range(int years, string unit)
    {
        if (unit == "year") return (years, years);
        if (unit == "month") return ((BigInteger)years * 12, (BigInteger)years * 12 + 11);
        var days = DayEnvelopes.GetOrAdd(years, y =>
        {
            BigInteger? low = null, upper = null;
            foreach (var anchor in Anchors())
            {
                var start = AnniversaryDays(anchor, y, false);
                var end = AnniversaryDays(anchor, (BigInteger)y + 1, false);
                low = low is null ? start : BigInteger.Min(low.Value, start);
                upper = upper is null ? end : BigInteger.Max(upper.Value, end);
            }
            return (low!.Value, upper!.Value - 1);
        });
        return unit switch
        {
            "week" => (days.Low / 7, days.High / 7),
            "hour" => (days.Low * 24, (days.High + 1) * 24 - 1),
            "minute" => (days.Low * 1440, (days.High + 1) * 1440 - 1),
            _ => days
        };
    }

    private static IEnumerable<DateOnly> Anchors()
    {
        var first = new DateOnly(2000, 1, 1);
        for (var day = 0; day < 146097; day++) yield return first.AddDays(day);
    }

    // Whole 400-year cycles avoid DateOnly/Int64 overflow even at Int64.MaxValue.
    private static BigInteger AnniversaryDays(DateOnly anchor, BigInteger count, bool months)
    {
        var cycle = months ? 4800 : 400;
        var cycles = BigInteger.DivRem(count, cycle, out var remainder);
        var anniversary = months ? anchor.AddMonths((int)remainder) : anchor.AddYears((int)remainder);
        return cycles * 146097 + anniversary.DayNumber - anchor.DayNumber;
    }

    private static BigInteger ThresholdMinutes(DateOnly anchor, Bound bound, bool afterMaximum)
    {
        var count = bound.Count + (afterMaximum ? 1 : 0);
        return bound.Unit switch
        {
            "year" => AnniversaryDays(anchor, count, false) * 1440,
            "month" => AnniversaryDays(anchor, count, true) * 1440,
            "week" => count * 10080,
            "day" => count * 1440,
            "hour" => count * 60,
            _ => count
        };
    }

    private static bool ValidForEveryAnchor(Bound min, Bound max)
    {
        if (min.Unit == max.Unit) return min.Count <= max.Count;
        foreach (var anchor in Anchors())
            if (ThresholdMinutes(anchor, min, false) >= ThresholdMinutes(anchor, max, true)) return false;
        return true;
    }
}
