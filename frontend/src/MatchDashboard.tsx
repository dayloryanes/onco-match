import { Fragment, useCallback, useEffect, useEffectEvent, useRef, useState } from 'react'
import { ApiError, matchApi } from './api'
import type { CurrentUser, ImportAttempt } from './api'
import ImportAttemptDetails from './ImportAttemptDetails'
import type { Evidence, LabelFinding, MatchBatch, MatchEvent, MatchPair, MatchState, PairDetails } from './matchTypes'
import type { PreserveReadingPosition } from './useReadingPosition'
import { patientSex, patientStage, classificationSystem, diseaseState } from './patientDisplay'
import { matchRequestMessage, stoppedMatchNotice } from './matchMessages'
import type { MatchNotice } from './matchMessages'
import { studySex } from './studyDisplay'

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
const pairKey = (pair: MatchPair) => `${pair.batchId}-${pair.patientId}-${pair.nctId}`

function MatchMessage({ notice }: { notice: MatchNotice }) {
  return <p className="match-message" role={notice.tone}>{notice.text}</p>
}
function conditionIndex(sourceId: string) {
  const match = /^study\.conditions\.(\d+)$/.exec(sourceId)
  return match ? Number(match[1]) : null
}
function sourceLabel(sourceId: string) {
  const index = conditionIndex(sourceId)
  if (index !== null) return `Study condition ${index + 1}`
  const labels: Record<string, string> = {
    'patient.ageYears': 'Patient age', 'patient.sex': 'Patient sex', 'patient.cancerType': 'Patient cancer type',
    'patient.stage.status': 'Patient stage status', 'patient.stage.value': 'Patient stage at diagnosis',
    'patient.stage.classificationSystem': 'Patient stage classification system',
    'patient.diseaseState.status': 'Patient disease state status', 'patient.diseaseState.value': 'Patient disease state',
  }
  return labels[sourceId] ?? sourceId
}
function EvidenceList({ evidence }: { evidence: Evidence[] }) {
  return evidence.length > 0 && <ul className="assessment-evidence">{evidence.map((item, i) => <li key={i}><span className="fact-label">{sourceLabel(item.sourceId)}:</span> {item.quote ?? display(item.value)}</li>)}</ul>
}
function Finding({ title, finding }: { title: string; finding: LabelFinding }) {
  return <div className="assessment-stack"><h5>{title}: <span className="finding-result">{label(finding.result)}</span></h5><p>{finding.explanation}</p>
    <EvidenceList evidence={finding.patientEvidence} /><EvidenceList evidence={finding.studyEvidence} /></div>
}
function Assessment({ detail }: { detail: PairDetails }) {
  const snapshot = detail.inputSnapshot
  return <section className="assessment-detail" aria-label={`Assessment ${detail.patientId} ${detail.nctId}`}>
    <div className="assessment-content">
    <header className="assessment-stack">
    <h4 className="assessment-heading">Assessment: {detail.patientId} · {detail.nctId}</h4>
    <p>{label(detail.pairSummary)}</p>
    {detail.skipReason && <p>{detail.skipReason}</p>}
    </header>
    {detail.structuredChecks && <section className="assessment-major assessment-groups" aria-label="Age and sex checks">
      <div className="assessment-stack"><h5>Age check: <span className="finding-result">{label(detail.structuredChecks.age.result)}</span></h5><p>{detail.structuredChecks.age.explanation}</p></div>
      <div className="assessment-stack"><h5>Sex check: <span className="finding-result">{label(detail.structuredChecks.sex.result)}</span></h5><p>{detail.structuredChecks.sex.explanation}</p></div>
    </section>}
    {detail.conditionLabels?.map(condition => {
      const index = conditionIndex(condition.conditionLabelId)
      const text = index === null ? null : snapshot?.study.conditions?.[index]
      const examinedSources = [...new Set([condition.cancerType, condition.stage, condition.diseaseState].flatMap(finding => finding.examinedSources))]
      return <article key={condition.conditionLabelId} className="condition-assessment assessment-major assessment-groups">
      <header className="assessment-stack"><h4>{index === null ? condition.conditionLabelId : `Condition ${index + 1}`} · {text?.trim() ? text : 'Condition text not available'}</h4><p>{label(condition.result)}</p></header>
      <div className="assessment-stack"><h5>Requirement summary</h5><p>{condition.requirementSummary}</p></div>
      <div className="assessment-stack"><h5>Explanation</h5><p>{condition.explanation}</p></div>
      <Finding title="Cancer type" finding={condition.cancerType} /><Finding title="Stage" finding={condition.stage} /><Finding title="Disease state" finding={condition.diseaseState} />
      <section className="assessment-stack"><h5>Unassessed qualifiers</h5>{condition.unassessedQualifiers.length === 0 ? <p>None stated.</p> : condition.unassessedQualifiers.map((qualifier, i) => <div key={i} className="assessment-stack">
        <p><span className="fact-label">{qualifier.qualifier}</span>: {qualifier.reason}</p><EvidenceList evidence={qualifier.studyEvidence} /></div>)}</section>
      {examinedSources.length > 0 && <p className="attempt-id">Examined: {examinedSources.map(sourceLabel).join(', ')}</p>}
    </article>})}
    </div>
    {snapshot && <details className="assessment-major"><summary>Exact input snapshot</summary>
    <section className="snapshot-section" aria-label="Patient snapshot"><h5>Patient</h5><dl className="patient-facts">
      <div><dt>Age (completed years)</dt><dd>{display(snapshot.patient.ageYears)}</dd></div>
      <div><dt>Sex</dt><dd>{patientSex(snapshot.patient.sex)}</dd></div><div><dt>Cancer type</dt><dd>{display(snapshot.patient.cancerType)}</dd></div>
      <div><dt>Stage at diagnosis</dt><dd>{patientStage(snapshot.patient.stage)}</dd></div>
      <div><dt>Classification system</dt><dd>{classificationSystem(snapshot.patient.stage)}</dd></div>
      <div><dt>Disease state</dt><dd>{diseaseState(snapshot.patient.diseaseState)}</dd></div>
    </dl></section>
    <section className="snapshot-section" aria-label="Study snapshot"><h5>Study</h5><dl className="patient-facts">
      <div><dt>Age bounds</dt><dd>{display(snapshot.study.minimumAge)} — {display(snapshot.study.maximumAge)}</dd></div>
      <div><dt>Sex</dt><dd>{display(studySex(snapshot.study.sex))}</dd></div><div><dt>Conditions</dt><dd>{snapshot.study.conditions?.length ? <ul>{snapshot.study.conditions.map((condition, i) => <li key={i}>{condition.trim() ? condition : 'Not available'}</li>)}</ul> : 'Not available'}</dd></div>
    </dl></section></details>}
  </section>
}

export default function MatchDashboard({ user, onExpired, imports, importVersion, importReady, refreshId, preserveReadingPosition }: { user: CurrentUser; onExpired: () => void; imports?: ImportAttempt[]; importVersion?: string; importReady: boolean; refreshId: number; preserveReadingPosition: PreserveReadingPosition }) {
  const [state, setState] = useState<MatchState | null>(null)
  const [selected, setSelected] = useState<MatchBatch | null>(null)
  const [detail, setDetail] = useState<PairDetails | null>(null)
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [expandedPair, setExpandedPair] = useState<string | null>(null)
  const [inspectionError, setInspectionError] = useState('')
  const [error, setError] = useState('')
  const [batchNotices, setBatchNotices] = useState<Record<string, MatchNotice>>({})
  const [pending, setPending] = useState(false)
  const [cancelPending, setCancelPending] = useState(false)
  const refreshRequest = useRef<AbortController | null>(null)
  const stream = useRef<AbortController | null>(null)
  const selection = useRef<AbortController | null>(null)
  const detailRequest = useRef<AbortController | null>(null)
  const currentExpandedId = useEffectEvent(() => expandedId)
  const alive = useRef(true)

  useEffect(() => {
    alive.current = true
    return () => { alive.current = false; stream.current?.abort(); selection.current?.abort(); detailRequest.current?.abort() }
  }, [])

  const failure = useCallback((value: unknown, message: string, batchId?: string) => {
    if (value instanceof ApiError && value.status === 401) onExpired()
    else if (batchId) setBatchNotices(current => ({ ...current, [batchId]: { text: message, tone: 'alert' } }))
    else setError(message)
  }, [onExpired])

  useEffect(() => {
    if (!importReady) return
    selection.current?.abort()
    const controller = new AbortController()
    refreshRequest.current = controller
    const id = currentExpandedId()
    Promise.all([
      matchApi.batches(controller.signal),
      id && !stream.current ? matchApi.batch(id, controller.signal) : null,
    ]).then(([value, full]) => {
      if (controller.signal.aborted) return
      const apply = () => {
        // A running operation owns its live counts. Refresh can discover new
        // imports without replacing those counts with an older read response.
        setState(current => stream.current && current ? { ...value, activeOperation: current.activeOperation,
          batches: value.batches.map(item => current.batches.find(saved => saved.batchId === item.batchId) ?? { ...item, availableActions: [] }),
        } : { ...value, batches: value.batches.map(item => item.batchId === full?.batchId ? full : full?.activeOperation ? { ...item, availableActions: [] } : item) })
        if (full && !stream.current) setSelected(full)
        setError('')
        setBatchNotices({})
      }
      if (refreshId > 0) preserveReadingPosition(apply)
      else apply()
    }).catch(value => {
      if (controller.signal.aborted) return
      const apply = () => failure(value, 'Unable to refresh assessments. Use Refresh to try again.')
      if (refreshId > 0) preserveReadingPosition(apply)
      else apply()
    })
    return () => controller.abort()
  }, [importVersion, importReady, refreshId, failure, preserveReadingPosition])

  async function inspect(batch: MatchBatch) {
    if (expandedId === batch.batchId) { closeBatch(batch.batchId); return }
    setExpandedId(batch.batchId)
    refreshRequest.current?.abort(); selection.current?.abort(); detailRequest.current?.abort()
    if (pending && selected?.batchId === batch.batchId) return
    const controller = new AbortController(); selection.current = controller
    setDetail(null); setExpandedPair(null); setError('')
    try {
      const value = await matchApi.batch(batch.batchId, controller.signal)
      if (!controller.signal.aborted) {
        setSelected(value)
        setState(current => current && ({ ...current, batches: current.batches.map(item => item.batchId === value.batchId
          ? value : value.activeOperation ? { ...item, availableActions: [] } : item) }))
      }
    }
    catch (value) { if (!controller.signal.aborted) failure(value, 'Unable to load this batch. Use Refresh to try again.', batch.batchId) }
  }
  function closeBatch(id: string) {
    refreshRequest.current?.abort(); selection.current?.abort(); detailRequest.current?.abort()
    setExpandedId(null); setDetail(null); setExpandedPair(null)
    document.getElementById(`batch-toggle-${id}`)?.focus()
  }
  async function openPair(pair: MatchPair) {
    if (expandedPair === pairKey(pair)) { closePair(pair); return }
    detailRequest.current?.abort()
    const controller = new AbortController(); detailRequest.current = controller
    setExpandedPair(pairKey(pair)); setDetail(null); setInspectionError('')
    try { const value = await matchApi.pair(pair.patientId, pair.nctId, controller.signal); if (!controller.signal.aborted) setDetail(value) }
    catch (value) {
      if (controller.signal.aborted) return
      if (value instanceof ApiError && value.status === 401) onExpired()
      else setInspectionError('Unable to load this assessment. Close and inspect again to retry.')
    }
  }
  function closePair(pair: MatchPair) {
    detailRequest.current?.abort()
    setExpandedPair(null); setDetail(null); setInspectionError('')
    document.getElementById(`inspection-toggle-${pairKey(pair)}`)?.focus()
  }
  async function reconcile(id: string, signal: AbortSignal) {
    try {
      const [current, batch] = await Promise.all([matchApi.batches(signal), matchApi.batch(id, signal)])
      if (!signal.aborted) { setState(current); setSelected(batch) }
    } catch (value) { if (!signal.aborted) { setState(null); setSelected(null); failure(value, 'Unable to refresh saved results. Use Refresh before taking another action.', id) } }
  }
  async function start(batch: MatchBatch) {
    if (stream.current) return
    refreshRequest.current?.abort()
    selection.current?.abort()
    detailRequest.current?.abort()
    const controller = new AbortController(); stream.current = controller
    setExpandedId(batch.batchId)
    setPending(true); setError(''); setDetail(null); setExpandedPair(null)
    setBatchNotices(current => { const next = { ...current }; delete next[batch.batchId]; return next })
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
        if (event.type === 'operationStopped') setBatchNotices(current => ({ ...current, [batch.batchId]: stoppedMatchNotice(event.stopReason) }))
      })
    } catch (value) {
      if (controller.signal.aborted) return
      const fallback = value instanceof ApiError
        ? value.message === value.code ? 'Unable to start match. Use Refresh to check the batch status.' : `${value.message.replace(/[.!?]+$/, '')}.`
        : 'Unable to confirm live progress. Use Refresh to check saved results. No action was resubmitted.'
      failure(value, value instanceof ApiError ? matchRequestMessage(value.code, fallback) : fallback, batch.batchId)
    } finally {
      refreshRequest.current?.abort()
      if (!controller.signal.aborted) { await reconcile(batch.batchId, controller.signal); setPending(false) }
      refreshRequest.current?.abort()
      if (stream.current === controller) stream.current = null
    }
  }
  async function cancel(batch: MatchBatch) {
    if (cancelPending) return
    setCancelPending(true)
    try { await matchApi.cancel(batch.activeOperation!.operationId); if (alive.current) setBatchNotices(current => ({ ...current, [batch.batchId]: { text: 'Cancellation requested. Waiting for active work to stop.', tone: 'status' } })) }
    catch (value) { if (alive.current) failure(value, value instanceof ApiError ? matchRequestMessage(value.code, 'Unable to confirm cancellation. Use Refresh to check the match status.') : 'Unable to confirm cancellation. Use Refresh to check the match status.', batch.batchId) }
    finally { if (alive.current) setCancelPending(false) }
  }

  const batches = state?.batches ?? []
  const rows = [
    ...(imports ?? []).map(attempt => ({ id: attempt.id, attempt, batch: batches.find(batch => batch.batchId === attempt.id) })),
    ...batches.filter(batch => !imports?.some(attempt => attempt.id === batch.batchId)).map(batch => ({ id: batch.batchId, attempt: undefined, batch })),
  ]
  return <section aria-label="Import history and matching" className="history-section">
    {error && <MatchMessage notice={{ text: error, tone: 'alert' }} />}
    {Object.entries(batchNotices).filter(([id]) => !rows.some(row => row.id === id)).map(([id, notice]) => <MatchMessage key={id} notice={notice} />)}
    {!state && !error && <p role="status">Loading match batches…</p>}
    {state?.configurationError && <MatchMessage notice={{ text: `Match configuration: ${state.configurationError}`, tone: 'alert' }} />}
    {imports?.length === 0 && rows.length === 0 && <p>No import attempts yet.</p>}
    <ol className="import-history">{rows.map(({ id, attempt, batch }) => {
      const number = batch ? batches.findIndex(item => item.batchId === batch.batchId) + 1 : null
      const expanded = !!batch && expandedId === batch.batchId
      return <li className="import-entry" key={id} aria-labelledby={attempt ? `import-attempt-${id}` : `batch-heading-${id}`}>
        {batchNotices[id] && <MatchMessage notice={batchNotices[id]} />}
        <div className="import-attempt">
        <div className={batch || attempt?.status === 'completed' ? 'import-batch-columns' : undefined}>
          {attempt ? <ImportAttemptDetails attempt={attempt} /> : <div className="import-details"><h3>Completed import</h3><p>Refresh import history to load this import's details.</p></div>}
          {batch ? <div className="batch-summary">
            <h3 id={`batch-heading-${id}`}>Batch {number} · {label(batch.status)}</h3>
            <p><strong>{batch.completedPairCount}/{batch.totalPairCount} completed</strong> · {batch.failedPairCount} failed · {batch.unfinishedPairCount} unfinished · {batch.activePairCount} active</p>
            <div className="actions">
              {user.role === 'Operator' && batch.availableActions.includes('start') && <button disabled={pending} onClick={() => start(batch)}>Start match</button>}
              {batch.activeOperation?.canCancel && user.role === 'Operator' && <button disabled={cancelPending} onClick={() => cancel(batch)}>{cancelPending ? 'Requesting cancellation…' : 'Cancel match'}</button>}
              <button id={`batch-toggle-${id}`} className="quiet-button" aria-expanded={expanded} aria-controls={`batch-results-${id}`} disabled={pending && selected?.batchId !== batch.batchId} onClick={() => inspect(batch)}>{expanded ? 'Close batch' : 'View batch'} {number}</button>
            </div>
          </div> : attempt?.status === 'completed' && <div className="batch-summary"><p>Loading the matching batch. If it remains unavailable, use Refresh.</p></div>}
        </div>
    {batch && expandedId === batch.batchId && <section id={`batch-results-${batch.batchId}`} className="batch-results" aria-label="Batch assessments">
      <h3>Batch assessments · {label(selected?.batchId === batch.batchId ? selected.status : batch.status)}</h3>
      <p className="notice">{limits}</p>
      {selected?.batchId === batch.batchId && selected.pairs && <div className="table-scroll"><table><thead><tr><th>Patient</th><th>Study</th><th>Assessment</th><th>Details</th></tr></thead>
        <tbody>{selected.pairs.map(pair => <Fragment key={pairKey(pair)}><tr><th scope="row">{pair.patientId}</th><td>{pair.nctId}</td>
          <td>{label(pair.pairSummary ?? pair.state)}{pair.latestAttempt?.failureExplanation && <> · {label(pair.latestAttempt.failureExplanation)}</>}</td>
          <td>{pair.state === 'completed' && <button id={`inspection-toggle-${pairKey(pair)}`} className="quiet-button" aria-expanded={expandedPair === pairKey(pair)} aria-controls={`inspection-${pairKey(pair)}`} onClick={() => openPair(pair)}>{expandedPair === pairKey(pair) ? 'Close details' : 'Inspect'} {pair.patientId} {pair.nctId}</button>}</td></tr>
          {expandedPair === pairKey(pair) && <tr className="assessment-row"><td colSpan={4}><div id={`inspection-${pairKey(pair)}`}>
            {detail && pairKey(detail) === pairKey(pair) ? <Assessment detail={detail} />
              : inspectionError ? <p role="alert" className="match-message inspection-message">{inspectionError}</p> : <p role="status" className="inspection-message">Loading assessment…</p>}
            <div className="actions assessment-actions"><button className="quiet-button" onClick={() => closePair(pair)}>Close assessment</button></div>
          </div></td></tr>}
        </Fragment>)}</tbody></table></div>}
      <div className="actions batch-results-actions"><button className="quiet-button" onClick={() => closeBatch(batch.batchId)}>Collapse results</button></div>
    </section>}
        </div>
      </li>
    })}</ol>
  </section>
}
