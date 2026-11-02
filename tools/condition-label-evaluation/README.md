# Condition-label evaluation runner

One isolated development console for the selected original v5 prompt plus both
original result-field descriptions, GPT-OSS 20B low through native Ollama.
The app does not reference this project. Live execution remains disabled.
Prompt optimization is deferred; all 19 acceptance cases remain reserved.

## Assets and verification

- `baseline/`: 21 unchanged assets: prompt, full/subset settings, pinned model
  metadata, development inputs and all 16 frozen requests.
- `baseline.sha256.json` and `audit-baseline.cjs`: portable byte-integrity check.
- `Labels.cs` and `SchemaGuidance.cs`: retained schema, validator, pair aggregation
  and original descriptions. Unused narrative/Qwen code has been removed.
- `results/`, `bin/`, `obj/`: ignored, disposable outputs.

From the repository root, with the .NET 10 SDK:

```powershell
dotnet run --project tools/condition-label-evaluation -- prepare
dotnet run --project tools/condition-label-evaluation -- self-check
node tools/condition-label-evaluation/audit-baseline.cjs
```

Node is required only for the byte-integrity audit. Preparation and self-check
need no Ollama service, network, frontend dependencies or `.local/` directory.
Run prepare again after source changes because previews are fingerprint-bound.
Preparation compares all 16 requests, including schema order and both messages,
against frozen full-low requests. The fixture loader reads only the approved
development section and compares it with frozen inputs. Self-check uses fake
HTTP transport and synthetic answers, including intentional failures; its
progress messages do not represent live inference or semantic validation.

## Status and references

All previous live approvals are consumed. The production guard rejects live
execution even with the historical run ID. Approval/identity guards, guarded
recording, privacy protections and fake-transport checks remain for future work.
A future live run requires a reviewed execution design, new run identity and
fresh human approval before enabling it. No inference is authorized by cleanup.

See [baseline and findings](../../docs/evaluation/condition-label-baseline.md) for
the exact configuration, outcomes, limitations and model/code boundary.
Raw historical evidence and local audits live under the ignored workspace
directory `.local/condition-label-evaluation/`; they are supplementary and never
required to understand the report or run these offline checks.
