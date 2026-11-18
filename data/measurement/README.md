# Measuring the selection rule

The claim: every figure in a report comes from the SEC filing it links to, and the rule that picks it is measured
against values read by hand from each 10-K, not from the XBRL facts the rule reads.

## What is compared

- **The current rule:** `SecFactSelector` and `HybridFinancialStatementProvider`, run through the production provider
  as a report would run them, as of the capture date.
- **The rule before the fix:** the selection Proofsheet used until commit `ea4d756`, ported unchanged from commit
  `3dc6852` into the test project as a baseline (`Proofsheet.Tests.Unit/Measurement/PreFixSelectionRule.cs`). Nothing in
  the app uses it.
- **The truth:** for each company, seven values read by hand from the latest 10-K filed by the capture date, each with
  the accession number, the statement, the line label and the column it was read from (`truth/<TICKER>.json`).

Both rules read the same captured company facts (`data/sec/<capture date>`).

## How a result is counted

- **Built:** the rule produced a statement. **Refused:** it did not; a refusal is no answer, not a correct one, and is
  listed with its reason.
- **Fully correct:** the statement's fiscal year is the 10-K's, and all seven figures equal the 10-K's at the precision
  the 10-K prints them (usually millions of US dollars, so the rule's exact value is rounded to the million). A wrong
  fiscal year makes every figure wrong.
- The seven figures: revenue and the prior year's revenue, operating income, operating cash flow, capital expenditure,
  cash and cash equivalents, and debt.

## What each value means, written down before reading any 10-K

- **Revenue:** the income statement's total revenue line ("Total net sales", "Total revenues"), for the fiscal year and
  for the comparative prior year in the same 10-K.
- **Operating income:** the income statement's operating income line. A 10-K without one has no value, and a rule that
  builds a statement for it is wrong.
- **Operating cash flow:** the cash flow statement's net cash from operating activities.
- **Capital expenditure:** the cash flow statement's cash paid for property, plant and equipment, as a positive number;
  not additions accrued but unpaid.
- **Cash:** the balance sheet's "Cash and cash equivalents" at the fiscal year end; restricted cash only where the
  balance sheet itself combines it into that line.
- **Debt:** the sum of every borrowing line on the balance sheet at the fiscal year end: commercial paper, short-term
  borrowings, the current portion of long-term debt, and long-term debt. Finance lease liabilities count only where the
  balance sheet folds them into a debt line; operating lease liabilities never count. Each line is recorded with its
  value, so the sum can be checked.

## Files

| File | What it is |
|---|---|
| `companies.json` | The list, its selection rule in words, and the capture date. |
| `truth/<TICKER>.json` | The hand-read values for one company, with where each was printed. |
| `results.md` | Generated. `dotnet test` fails when a fresh run differs from it. |

To regenerate `results.md` after a deliberate change, set `PROOFSHEET_UPDATE_MEASUREMENT=1`, run
`dotnet test --filter SelectionMeasurementTests`, and review the diff before committing it.

## Status on 2026-11-18

- **No 10-K has been read yet**, so `results.md` shows only what each rule built or refused. Reading the filings needs
  live access to SEC EDGAR, which waits on the owner's choice of contact for SEC's User-Agent rule.
- `companies.json` holds an interim list: the 14 companies recorded on 2026-11-06, each chosen for a known edge case.
  The measurement list itself waits on the owner's choice.
- The 2026-11-06 recording was trimmed to the concepts the current rule reads, plus two capital-expenditure concepts.
  The rule before the fix also read `LongTermDebtAndFinanceLeaseObligations`,
  `LongTermDebtAndCapitalLeaseObligationsNoncurrent` and `LongTermDebtAndFinanceLeaseObligationsNoncurrent`, so on this
  recording its debt, and some of its refusals, may differ from what it did on full data. Later captures keep every
  concept either rule reads (`data/sec/reference.js`).
- The count quoted before this measurement, "27 of the 35 statements it could build had at least one wrong figure"
  (2026-11-06, 52 companies), compared the two rules with each other, not with the filings, and its data was not kept.
