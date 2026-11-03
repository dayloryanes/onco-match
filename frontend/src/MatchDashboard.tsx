import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, matchApi } from './api'
import type { CurrentUser } from './api'
import type { Evidence, LabelFinding, MatchBatch, MatchEvent, MatchPair, MatchState, PairDetails } from './matchTypes'

const names: Record<string, string> = {
  noWork: 'No work', notStarted: 'Not started', running: 'Running', stopping: 'Stopping', unfinished: 'Unfinished',
  finishedWithFailures: 'Finished with failures', completed: 'Completed', failed: 'Failed',
  excludedByAgeSex: 'Excluded by age/sex', conditionLabelAlignmentSupported: 'Condition-label alignment supported',
  mixedConditionLabelFindings: 'Mixed condition-label findings', conditionLabelConflictsIdentified: 'Condition-label conflicts identified',
  conditionLabelAlignmentUndetermined: 'Condition-label alignment undetermined', alignmentSupported: 'Alignment supported',
  conflictIdentified: 'Conflict identified', undetermined: 'Undetermined', notSpecified: 'Not specified in condition labels',
  satisfied: 'Satisfied', excluded: 'Excluded', noRestrictionIdentified: 'No restriction identified',
  sessionExpired: 'Session expired', disconnected: 'Connection lost', cancelled: 'Cancelled', logout: 'Logged out',
  inferenceUnavailable: 'The selected local inference service or model is unavailable. Check setup before continuing.',
  internalError: 'The operation stopped after an unexpected error.',
}
const label = (value: string | null) => value ? names[value] ?? value : 'Not assessed'
const limits = "This assessment checks structured age and sex requirements and compares cancer type, stage, and disease state with the study's listed conditions. Biomarkers, treatment history, other requirements, and free-text eligibility criteria are not assessed. Results do not establish clinical eligibility or enrollment approval."
const display = (value: unknown) => value === null || value === undefined ? 'Unknown' : typeof value === 'string' ? value : JSON.stringify(value)

function EvidenceList({ evidence }: { evidence: Evidence[] }) {
  return evidence.length > 0 && <ul className="assessment-evidence">{evidence.map((item, i) => <li key={i}><span className="fact-label">{item.sourceId}:</span> {item.quote ?? display(item.value)}</li>)}</ul>
}
function Finding({ title, finding }: { title: string; finding: LabelFinding }) {
  return <div><h5>{title}: {label(finding.result)}</h5><p>{finding.explanation}</p>
    <EvidenceList evidence={finding.patientEvidence} /><EvidenceList evidence={finding.studyEvidence} />
    {finding.examinedSources.length > 0 && <p className="attempt-id">Examined: {finding.examinedSources.join(', ')}</p>}</div>
}
function Assessment({ detail, close }: { detail: PairDetails; close: () => void }) {
  const snapshot = detail.inputSnapshot
  return <section className="assessment-detail" aria-label={`Assessment ${detail.patientId} ${detail.nctId}`}>
    <div className="actions"><h4>Assessment: {detail.patientId} · {detail.nctId}</h4><button className="quiet-button" onClick={close}>Close assessment</button></div>
    <p><strong>{label(detail.pairSummary)}</strong></p><p className="notice">{detail.assessmentLimits}</p>
    {detail.skipReason && <p>{detail.skipReason}</p>}
    {detail.structuredChecks && <><h5>Structured age: {label(detail.structuredChecks.age.result)}</h5><p>{detail.structuredChecks.age.explanation}</p>
      <h5>Structured sex: {label(detail.structuredChecks.sex.result)}</h5><p>{detail.structuredChecks.sex.explanation}</p></>}
    {detail.conditionLabels?.map(condition => <article key={condition.conditionLabelId} className="condition-assessment">
      <h4>{condition.requirementSummary}</h4><p><strong>{label(condition.result)}</strong></p><p>{condition.explanation}</p>
      <Finding title="Cancer type" finding={condition.cancerType} /><Finding title="Stage" finding={condition.stage} /><Finding title="Disease state" finding={condition.diseaseState} />
      <h5>Unassessed qualifiers</h5>{condition.unassessedQualifiers.length === 0 ? <p>None stated.</p> : condition.unassessedQualifiers.map((qualifier, i) => <div key={i}>
        <p><strong>{qualifier.qualifier}</strong>: {qualifier.reason}</p><EvidenceList evidence={qualifier.studyEvidence} /></div>)}
    </article>)}
    {snapshot && <details><summary>Exact input snapshot</summary><dl className="patient-facts">
      <div><dt>Patient age (completed years)</dt><dd>{display(snapshot.patient.ageYears)}</dd></div>
      <div><dt>Patient sex</dt><dd>{snapshot.patient.sex}</dd></div><div><dt>Cancer type</dt><dd>{display(snapshot.patient.cancerType)}</dd></div>
      <div><dt>Stage</dt><dd>{snapshot.patient.stage.status} · {display(snapshot.patient.stage.value)} · {display(snapshot.patient.stage.classificationSystem)}</dd></div>
      <div><dt>Disease state</dt><dd>{snapshot.patient.diseaseState.status} · {display(snapshot.patient.diseaseState.value)}</dd></div>
      <div><dt>Study age bounds</dt><dd>{display(snapshot.study.minimumAge)} — {display(snapshot.study.maximumAge)}</dd></div>
      <div><dt>Study sex</dt><dd>{display(snapshot.study.sex)}</dd></div><div><dt>Study conditions</dt><dd><ol start={0}>{snapshot.study.conditions?.map((condition, i) => <li key={i}>{condition || '(blank)'}</li>)}</ol></dd></div>
    </dl></details>}
  </section>
}

export default function MatchDashboard({ user, onExpired, importVersion, importReady, refreshId }: { user: CurrentUser; onExpired: () => void; importVersion?: string; importReady: boolean; refreshId: number }) {
  const [state, setState] = useState<MatchState | null>(null)
  const [selected, setSelected] = useState<MatchBatch | null>(null)
  const [detail, setDetail] = useState<PairDetails | null>(null)
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const [cancelPending, setCancelPending] = useState(false)
  const [refresh, setRefresh] = useState(0)
  const stream = useRef<AbortController | null>(null)
  const selection = useRef<AbortController | null>(null)
  const detailRequest = useRef<AbortController | null>(null)
  const alive = useRef(true)

  useEffect(() => {
    alive.current = true
    return () => { alive.current = false; stream.current?.abort(); selection.current?.abort(); detailRequest.current?.abort() }
  }, [])

  const failure = useCallback((value: unknown, message: string) => {
    if (value instanceof ApiError && value.status === 401) onExpired()
    else setError(message)
  }, [onExpired])

  useEffect(() => {
    if (stream.current || !importReady) return
    const controller = new AbortController()
    matchApi.batches(controller.signal).then(value => { if (!controller.signal.aborted) setState(value) })
      .catch(value => { if (!controller.signal.aborted) { setState(null); failure(value, 'Unable to load match batches. Refresh assessments to try again.') } })
    return () => controller.abort()
  }, [importVersion, importReady, refreshId, refresh, failure])

  async function inspect(batch: MatchBatch) {
    selection.current?.abort(); detailRequest.current?.abort()
    const controller = new AbortController(); selection.current = controller
    setDetail(null); setError('')
    try { const value = await matchApi.batch(batch.batchId, controller.signal); if (!controller.signal.aborted) setSelected(value) }
    catch (value) { if (!controller.signal.aborted) failure(value, 'Unable to load batch details.') }
  }
  async function openPair(pair: MatchPair) {
    detailRequest.current?.abort()
    const controller = new AbortController(); detailRequest.current = controller
    try { const value = await matchApi.pair(pair.patientId, pair.nctId, controller.signal); if (!controller.signal.aborted) setDetail(value) }
    catch (value) { if (!controller.signal.aborted) failure(value, 'Unable to load assessment details.') }
  }
  async function reconcile(id: string, signal: AbortSignal) {
    try {
      const [current, batch] = await Promise.all([matchApi.batches(signal), matchApi.batch(id, signal)])
      if (!signal.aborted) { setState(current); setSelected(batch) }
    } catch (value) { if (!signal.aborted) { setState(null); setSelected(null); failure(value, 'Unable to reconcile saved work. Refresh assessments before taking another action.') } }
  }
  async function start(batch: MatchBatch) {
    if (stream.current) return
    selection.current?.abort()
    const controller = new AbortController(); stream.current = controller
    setPending(true); setError(''); setDetail(null)
    try {
      const full = await matchApi.batch(batch.batchId, controller.signal)
      if (controller.signal.aborted) return
      setSelected(full)
      await matchApi.start(batch, controller.signal, (event: MatchEvent) => {
        if (controller.signal.aborted) return
        const activeOperation = event.type === 'operationFinished' || event.type === 'operationStopped' ? null : {
          operationId: event.operationId, action: 'start', startedAt: event.serverTime, selectedPairCount: event.totalPairCount, canCancel: true,
        }
        const update = (current: MatchBatch): MatchBatch => ({ ...current, ...event, activeOperation, availableActions: [],
          pairs: current.pairs?.map(pair => pair.patientId !== event.patientId || pair.nctId !== event.nctId ? pair : {
            ...pair, state: event.type === 'pairStarted' ? 'running' : event.type === 'pairCompleted' ? 'completed' : event.type === 'pairFailed' ? 'failed' : pair.state,
            pairSummary: event.pairSummary, structuredChecks: event.structuredChecks,
            latestAttempt: { status: event.type, failureCode: event.failureCode, failureExplanation: event.failureExplanation },
          }) ?? null })
        setState(current => current && ({ ...current, activeOperation, batches: current.batches.map(item => item.batchId === batch.batchId ? update(item) : { ...item, availableActions: [] }) }))
        setSelected(current => current?.batchId === batch.batchId ? update(current) : current)
        if (event.type === 'operationStopped') setError(`Operation stopped. ${label(event.stopReason)} Saved work is retained.`)
      })
    } catch (value) {
      if (controller.signal.aborted) return
      failure(value, value instanceof ApiError ? `${value.message}. Saved state has been refreshed.` : 'Unable to confirm live progress. Saved state has been refreshed; no action was resubmitted.')
    } finally {
      if (!controller.signal.aborted) { await reconcile(batch.batchId, controller.signal); setPending(false) }
      if (stream.current === controller) stream.current = null
    }
  }
  async function cancel() {
    const operation = selected?.activeOperation
    if (!operation || cancelPending) return
    setCancelPending(true)
    try { await matchApi.cancel(operation.operationId); if (alive.current) setError('Cancellation requested. Waiting for active work to stop.') }
    catch (value) { if (alive.current) failure(value, 'Unable to confirm cancellation. Refresh assessments to inspect saved state.') }
    finally { if (alive.current) setCancelPending(false) }
  }

  return <section aria-labelledby="match-heading" className="history-section">
    <h2 id="match-heading">Match batches</h2>
    <p>Run a completed import against the ten fictional patients. Results are shared until server restart.</p>
    <button className="quiet-button" disabled={pending} onClick={() => { setError(''); setSelected(null); setDetail(null); setRefresh(value => value + 1) }}>Refresh assessments</button>
    {error && <p role="alert">{error}</p>}
    {!state && !error && <p role="status">Loading match batches…</p>}
    {state?.configurationError && <p role="alert">Match configuration: {state.configurationError}</p>}
    {state?.batches.length === 0 && <p>No completed imports to assess.</p>}
    {state?.batches.map((batch, i) => <article className="import-attempt" key={batch.batchId} aria-label={`Match batch ${i + 1}`}>
      <h3>Batch {i + 1} · {label(batch.status)}</h3>
      <p><strong>{batch.completedPairCount}/{batch.totalPairCount} completed</strong> · {batch.failedPairCount} failed · {batch.unfinishedPairCount} unfinished · {batch.activePairCount} active</p>
      <div className="actions">
        {user.role === 'Operator' && batch.availableActions.includes('start') && <button disabled={pending} onClick={() => start(batch)}>Start match</button>}
        <button className="quiet-button" disabled={pending && selected?.batchId !== batch.batchId} onClick={() => inspect(batch)}>View batch {i + 1}</button>
      </div>
    </article>)}
    {selected && <section aria-label="Batch assessments">
      <h3>Batch assessments · {label(selected.status)}</h3>
      <p className="notice">{limits}</p>
      {selected.activeOperation?.canCancel && user.role === 'Operator' && <button disabled={cancelPending} onClick={cancel}>{cancelPending ? 'Requesting cancellation…' : 'Cancel match'}</button>}
      {selected.pairs && <div className="table-scroll"><table><thead><tr><th>Patient</th><th>Study</th><th>Assessment</th><th>Details</th></tr></thead>
        <tbody>{selected.pairs.map(pair => <tr key={`${pair.patientId}/${pair.nctId}`}><th scope="row">{pair.patientId}</th><td>{pair.nctId}</td>
          <td>{label(pair.pairSummary ?? pair.state)}{pair.latestAttempt?.failureExplanation && <p>{pair.latestAttempt.failureExplanation}</p>}</td>
          <td>{pair.state === 'completed' && <button className="quiet-button" onClick={() => openPair(pair)}>Inspect {pair.patientId} {pair.nctId}</button>}</td></tr>)}</tbody></table></div>}
      {detail && <Assessment detail={detail} close={() => { detailRequest.current?.abort(); setDetail(null) }} />}
    </section>}
  </section>
}
