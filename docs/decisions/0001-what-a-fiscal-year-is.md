# 0001. What a fiscal year is

Written on 2026-11-18, after the decision, from the code and the commit history. The decision was made in commit `ea4d756` on 2026-11-06.

## Context

SEC company facts give each fact the form and fiscal period (`fp`) of the filing that reported it, not of the period the
fact covers. A 10-K reports the year and its fourth quarter with the same end date, both tagged `fp=FY`. A 10-Q can
report trailing-twelve-month figures, which last a year but are not a fiscal year. Some companies use 52/53-week years
(364 or 371 days), and later filings restate a year or re-tag it with dates shifted by a day or two.

The first version counted any fact from an annual form, tagged `fp=FY`, or lasting 300 days or more as a year, and took
the two with the latest end dates. Apple came out with its FY2018 fourth quarter as the prior year (+322% revenue
growth), and Microsoft as FY2010.

## Decision

A fiscal year is a fact from an annual report (10-K, 10-KT, 20-F, 40-F, or an amendment of one) that spans 350 to 380
days. When the same period is reported again, the latest filing wins, and a year re-tagged within a week of the target
counts as the same year. Every figure in a statement must describe the same fiscal year as revenue. Since 2026-11-18 a
report also has an as-of date, and only facts filed on or before it count.

## Consequences

- Fourth quarters inside 10-Ks and trailing twelve months inside 10-Qs are never mistaken for a year.
- Restatements are followed, but not past the as-of date, so the same date always gives the same figures.
- A fiscal year of a length outside 350 to 380 days (a transition period, a first short year) is not a year; such
  companies are refused.

## Considered

- `fp=FY` alone: the fourth quarter carries it too.
- XBRL `frame` values: they are calendar-aligned (`CY2025`), exist for only some facts, and do not follow fiscal years.
- A shorter minimum such as 300 days: lets in the shapes above.

## Evidence

`Proofsheet.Infrastructure/Clients/SecFactSelector.cs`, `SecFactSelectorTests`, and the recorded companies in
`data/sec` (AAPL, MSFT, AMZN and COST exercise each case). `data/measurement/results.md` shows the rule before and after
on the same data.
