# Condition-label development baseline

The selected development baseline is the original version 5 prompt plus the two
original result-field schema descriptions, GPT-OSS 20B at low reasoning through
native Ollama. Optimization is deferred while the application is completed.
Condition-label assessment is not yet integrated into the delivered application.

The isolated runner now supports offline preparation and fake-transport checks
for native and OpenAI-compatible local Ollama requests. The compatible candidate
uses an offline declaration that does not attest to actual installed identity. That
extension does not change the selected native baseline or its historical results;
no compatible-route acceptance is claimed. A separately approved CL-D01 smoke
completed once per route with low reasoning: native 97.836 seconds, compatible
52.541 seconds, both mechanically valid and matching the expected findings.
This is one-case integration evidence, not a controlled performance comparison
or quality gate. The smoke used a separately named compatible preset with the
same weights/template and explicit model parameters; it leaves all 19 acceptance
fixtures reserved. Both live commands are disabled after consuming that approval.
See the runner README for both routes, evidence and execution restrictions.

This is a working development choice, not an accepted or clinically validated
configuration. All 19 acceptance cases remain reserved. The limitations below
remain material even when an answer passes mechanical validation.

## Relevant documentation

- [Current POC scope](../planning/poc-1-baseline.md)
- [Approved assessment contract](../planning/condition-label-assessment-design.md)
- [Approved evaluation cases and quality gate](../planning/condition-label-evaluation-design.md)
- [Evaluation runner and offline checks](../../tools/condition-label-evaluation/README.md)

## Exact selected baseline

All runner paths below are relative to `tools/condition-label-evaluation/`.

| Item | Retained value |
| --- | --- |
| Instructions | `baseline/prompt.txt`, original v5, 945 words |
| Instruction SHA256 | `f84d6f8f17dd5f71c7b2dc8b4f0e4b3b712a8c53a21ad4e0c5017ac881311a12` |
| Guidance | `SchemaGuidance.cs`, original Direct and Whole descriptions, 116 prose words |
| Description fields | `$defs.finding.properties.result.description` and `$defs.label.properties.result.description` |
| Schema placement | Native API `format` and schema appended to system text |
| Model | `gpt-oss:20b`, `think: low`, native Ollama |
| Settings and identity | `baseline/configuration.json`; exact model/template/version pins retained |
| Inputs and requests | `baseline/development-inputs.json` and `baseline/requests/CL-D01.request.json` through `CL-D16.request.json` |
| Original full-low fingerprint | `d4af81e96a237358bb98060593c23eed1e705776c1d8a4a3a96a5f0817131882` |

Controls: context 24,576; combined reasoning/final generation allowance 8,192;
framing reserve 4,096; temperature 0.7; top-p 0.8; top-k 20; min-p 0; seed 42;
repeat penalty 1; presence penalty 0; keep-alive five minutes. Development order
is D1–D16, one sequential attempt per case, no automatic retries or repair,
600 seconds per request and 9,660 seconds overall. These are historical controls,
not execution approval. The production guard explicitly disables live inference.

The 21 baseline assets have unchanged bytes. Their portable hash manifest is
`baseline.sha256.json`. Historical source fingerprints remain identifiers of the
evaluated runs; cleanup produces a different source/preparation fingerprint.

## Observed baseline results

The six-case subset D1/D7/D12/D13/D14/D15 matched all expected result values in
two frozen runs: 6/6 mechanically valid and 6/6 result matches each, taking
547.660 seconds and 564.732 seconds. Their request and final-answer strings were
identical. That establishes limited same-input repeatability, not generalization.

The full-low run was 16/16 mechanically valid and 12/16 complete result matches.
Total request time was 1,276.233 seconds (21m16s), median 67.037 seconds, slowest
156.730 seconds, without timeout, transport error or retry.

| Development cases | Findings and limitations |
| --- | --- |
| D1, D2, D5, D10, D13–D15 | Result values match; no material issue identified in the recorded review |
| D3 | Broad cancer scope incorrectly treated as insufficient information; cancer/label/pair uncertainty wrong |
| D4 | Direct cancer uncertainty correct, but complete-label/pair alignment wrong |
| D6 | Values match; qualifier-absence wording overreaches to the whole study |
| D7 | Values match; earlier subset absence-wording defect did not recur |
| D8 | Values match; complete-label explanation sparse, with uncertainty explained in the stage finding |
| D9 | Direct qualifier absence correct, but label/pair uncertainty wrong |
| D11 | Values match, but required unsupported qualifier and scope-limit reason omitted |
| D12 | Values match, but required status citations omitted, branch evidence incomplete, and eligibility-language drift |
| D16 | Unclear relationship treated as mandatory conjunction; label/pair conflict wrong |

Mechanical validity, correct result values, authentic evidence, complete evidence
and correct explanations are separate measures. The existing validator does not
enforce every semantic/citation-completeness expectation. The recorded semantic
review was AI-assisted; no independent review, maintainer manual validation,
acceptance pass or clinical validity is claimed.

## Ownership boundary

Code computes structured age/sex checks, validates model responses, and derives
the final pair summary from valid complete-label results. The model supplies
per-label cancer/stage/state findings, complete-label interpretation/results,
requirement summaries, evidence and explanations. There is no extra model-generated
pair-level explanation paragraph in the contract.

No comparison/applicability redesign, code-generated absence wording, stricter
validator or additional patient fields were adopted during cleanup.


## Reproduction and assessment limits

The separate development console generates all 16 requests from retained source
assets and compares them with the frozen full-low requests, including messages
and schema order. It also checks the approved development inputs against frozen
fixtures. Its offline checks use fake HTTP transport and synthetic responses;
they do not measure model quality or consume acceptance cases. Raw historical
runs, drafts and local audits are not repository dependencies.

The historical full-low run is identified by the fingerprint above. Cleanup
changes paths and source fingerprints, while preserving baseline asset bytes
and active schema, request, validation and pair-aggregation behavior. Live
execution is disabled; the runner README explains the current boundary.

Superseded narrative designs and the old console were previously published in
[the earlier repository tree](https://github.com/dayloryanes/onco-match/tree/da8799de73b6bb324eb1e84eb746b925e7b70e0d).
Those historical trials do not establish condition-label acceptance.
