# 0003. How debt is totalled

Written on 2026-11-18, after the decision, from the code and the commit history. The decision was made in commit `ea4d756` on 2026-11-06.

## Context

Companies tag debt inconsistently. Some report a total (`DebtLongtermAndShorttermCombinedAmount`,
`LongTermDebtAndCapitalLeaseObligationsIncludingCurrentMaturities`, `DebtAndCapitalLeaseObligations`, `LongTermDebt`),
some only the parts, and the names mislead: `LongTermDebtAndCapitalLeaseObligations` is the non-current amount only,
and `LongTermDebtCurrent` leaves out commercial paper, which `DebtCurrent` includes. UnitedHealth's reported total is
lower than its balance sheet's long-term plus current debt.

## Decision

Debt at the fiscal year end is the larger of two candidates: the first reported total in the order above, and the
non-current part plus the current part (`DebtCurrent` first, so commercial paper counts). Balances come from annual
reports first, so a later 10-Q that re-tags the year-end column cannot change them. The report shows the fact or facts
used, each with its filing.

## Consequences

- Commercial paper and current maturities are counted where a total left them out (PG, UNH).
- Taking the larger value is a heuristic, not a definition: it can count finance leases where a total includes them,
  and it understates debt when a company splits its current debt over several concepts the rule does not add up.
- Whether it matches the balance sheet is what the measurement checks. Its truth is written down separately in
  `data/measurement/README.md`: the sum of every borrowing line on the balance sheet, with operating leases excluded.

## Considered

- Only `LongTermDebt`: leaves out commercial paper and short-term borrowings.
- Summing every debt concept: double counts, because totals and parts are often both reported.

## Evidence

`HybridFinancialStatementProvider.TryGetDebt`, the BA, PG and UNH cases in `data/sec/README.md` and
`SecFinancialStatementFixtureTests`, and `Debt_Made_Of_Two_Parts_Shows_Both_Parts_With_Their_Filings`.
