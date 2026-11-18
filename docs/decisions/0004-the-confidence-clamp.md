# 0004. The confidence clamp

Written on 2026-11-18, after the decision, from the code and the commit history. The decision dates from the first commit, `5614c0a`, on 2026-03-18.

## Context

When a model writes the report it also states a confidence and a signal. Nothing calibrates the model's number, and a
model can be confident on thin evidence.

## Decision

The backend computes a baseline confidence from the evidence: completeness, evidence quality, agreement between sources
and how much of the report cites them, less penalties for retries, missing sources, unresolved contradictions and a
fallback. The model's confidence is clamped to ten points either side of that baseline. When the evidence score is below
55, a Bullish or Bearish signal becomes Neutral. Both are recorded in the report (`boundedFrom`, `boundedTo`,
`overrideApplied`).

## Consequences

- The number a reader sees moves with the evidence, and the model can shift it only a little.
- The weights, the ten-point band and the threshold of 55 are design choices. Nothing measures whether the confidence
  or the signal predicts anything, and the project does not claim it does.
- On the recorded demo, Apple's report is Neutral: with market data, news and the filings list missing, its evidence
  score is 34.

## Considered

- Taking the model's confidence as given: uncalibrated.
- Dropping confidence and signal: considered on 2026-11-06 and not decided.

## Evidence

`StructuredReasoningMaterializer.TryMaterialize`, `DeterministicScoringCalculator.CalculateConfidence`, and
`ReportGateTests`, which fail when the band or the threshold is moved.
