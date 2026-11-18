# 0002. Refuse rather than guess

Written on 2026-11-18, after the decision, from the code and the commit history. The rule dates from commit `ea4d756` on 2026-11-06; the end of sample figures for real companies from commit `55e62cb` on 2026-11-18.

## Context

When the facts for a statement are incomplete, there is always something nearby to use instead: the previous year, a
concept the company stopped using, a figure from another period, or, as the app did until 2026-11-18, seeded or
generated figures. Each produces a statement that looks complete and is wrong.

## Decision

When the rule cannot build every figure for the same fiscal year from the concepts it knows, it builds nothing and says
why. That covers a missing figure (banks and oil majors report no `OperatingIncomeLoss`), newest revenue tagged with a
concept the rule does not read (it does not fall back a year), and a latest fiscal year more than 18 months before the
as-of date. Financial statements are critical, so the job fails with that reason. Sample data exists only for fictional
companies, and a real company never falls back to it.

## Consequences

- Fewer companies get a report: 11 of the 14 recorded companies. JPM, XOM and TSM are refused, with the reason.
- The measurement counts a refusal as no answer, never as correct (`data/measurement/README.md`).
- A refused report states the reason to the reader instead of showing nothing.

## Considered

- Falling back to the previous year or a stale concept: the failures the fix was made to end.
- Industry-specific definitions (net interest income for banks): a different statement, not this one; left for later.
- Sample or generated figures for a real company: misleading, and removed.

## Evidence

`HybridFinancialStatementProvider.TryBuildLiveStatement`, `HybridFallback`, the refusal tests in
`SecFinancialStatementFixtureTests`, and `RecordedModeTests.Recorded_Bank_Is_Refused_With_The_Reason`.
