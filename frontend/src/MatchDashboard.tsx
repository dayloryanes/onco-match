import { Fragment, useCallback, useEffect, useEffectEvent, useRef, useState } from 'react'
import { ApiError, matchApi } from './api'
import type { CurrentUser, ImportAttempt } from './api'
import ImportAttemptDetails from './ImportAttemptDetails'
import type { MatchBatch, MatchEvent, MatchPair, MatchState, PairDetails } from './matchTypes'
import type { PreserveReadingPosition } from './useReadingPosition'
import { matchRequestMessage, stoppedMatchNotice } from './matchMessages'
import type { MatchNotice } from './matchMessages'
import Assessment from './Assessment'
import { label, limits } from './matchDisplay'

const pairKey = (pair: MatchPair) => `${pair.batchId}-${pair.patientId}-${pair.nctId}`

function MatchMessage({ notice }: { notice: MatchNotice }) {
  return <p className="match-message" role={notice.tone}>{notice.text}</p>
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
  const [needsRefresh, setNeedsRefresh] = useState(false)
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
    else preserveReadingPosition(() => {
      if (batchId) setBatchNotices(current => ({ ...current, [batchId]: { text: message, tone: 'alert' } }))
      else setError(message)
    })
  }, [onExpired, preserveReadingPosition])

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
        if (!stream.current) setNeedsRefresh(false)
        setError('')
        setBatchNotices({})
      }
      preserveReadingPosition(apply)
    }).catch(value => {
      if (controller.signal.aborted) return
      const apply = () => failure(value, 'Unable to refresh assessments. Use Refresh to try again.')
      preserveReadingPosition(apply)
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
      if (!controller.signal.aborted) preserveReadingPosition(() => {
        setSelected(value)
        setState(current => current && ({ ...current, batches: current.batches.map(item => item.batchId === value.batchId
          ? value : value.activeOperation ? { ...item, availableActions: [] } : item) }))
      })
    }
    catch (value) { if (!controller.signal.aborted) failure(value, 'Unable to load this batch. Use Refresh to try again.', batch.batchId) }
  }
  function closeBatch(id: string) {
    refreshRequest.current?.abort(); selection.current?.abort(); detailRequest.current?.abort()
    setExpandedId(null); setDetail(null); setExpandedPair(null)
    document.getElementById(`batch-toggle-${id}`)?.focus()
  }
  const inspectedPair = selected?.pairs?.find(pair => pairKey(pair) === expandedPair)
  useEffect(() => {
    if (!inspectedPair) return
    const controller = new AbortController(); detailRequest.current = controller
    matchApi.pair(inspectedPair.patientId, inspectedPair.nctId, controller.signal).then(value => {
      if (!controller.signal.aborted) preserveReadingPosition(() => { setDetail(value); setInspectionError('') })
    }).catch(value => {
      if (controller.signal.aborted) return
      if (value instanceof ApiError && value.status === 401) onExpired()
      else preserveReadingPosition(() => setInspectionError('Unable to load this assessment. Close and inspect again to retry.'))
    })
    return () => controller.abort()
  }, [inspectedPair, onExpired, preserveReadingPosition])

  function openPair(pair: MatchPair) {
    if (expandedPair === pairKey(pair)) { closePair(pair); return }
    detailRequest.current?.abort()
    setExpandedPair(pairKey(pair)); setDetail(null); setInspectionError('')
  }
  function closePair(pair: MatchPair) {
    detailRequest.current?.abort()
    setExpandedPair(null); setDetail(null); setInspectionError('')
    document.getElementById(`inspection-toggle-${pairKey(pair)}`)?.focus()
  }
  async function reconcile(id: string, signal: AbortSignal) {
    try {
      const [current, batch] = await Promise.all([matchApi.batches(signal), matchApi.batch(id, signal)])
      if (!signal.aborted) preserveReadingPosition(() => { setState(current); setSelected(batch); setNeedsRefresh(false) })
    } catch (value) {
      if (!signal.aborted) preserveReadingPosition(() => {
        // A failed read cannot invalidate already committed results. Retain the
        // inspection, but require a confirmed Refresh before another mutation.
        setNeedsRefresh(true)
        failure(value, 'Unable to refresh saved results. Use Refresh before taking another action.', id)
      })
    }
  }
  async function operate(batch: MatchBatch, action: string) {
    if (stream.current || needsRefresh) return
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
      await matchApi.operate(batch, action, controller.signal, (event: MatchEvent) => {
        if (controller.signal.aborted) return
        const activeOperation = event.type === 'operationFinished' || event.type === 'operationStopped' ? null : {
          operationId: event.operationId, action, startedAt: event.serverTime,
          selectedPairCount: action === 'continue' ? batch.unfinishedPairCount : action === 'retryFailed' ? batch.failedPairCount : batch.totalPairCount, canCancel: true,
        }
        const update = (current: MatchBatch): MatchBatch => ({ ...current, ...event, activeOperation, availableActions: [],
          pairs: current.pairs?.map(pair => pair.patientId !== event.patientId || pair.nctId !== event.nctId ? pair : {
            ...pair, state: event.type === 'pairStarted' ? 'running' : event.type === 'pairCompleted' ? 'completed' : event.type === 'pairFailed' ? 'failed' : pair.state,
            pairSummary: event.pairSummary, structuredChecks: event.structuredChecks,
            latestAttempt: { status: event.type, failureCode: event.failureCode, failureExplanation: event.failureExplanation },
          }) ?? null })
        preserveReadingPosition(() => {
          setState(current => current && ({ ...current, activeOperation, batches: current.batches.map(item => item.batchId === batch.batchId ? update(item) : { ...item, availableActions: [] }) }))
          setSelected(current => current?.batchId === batch.batchId ? update(current) : current)
          if (event.type === 'operationStopped') setBatchNotices(current => ({ ...current, [batch.batchId]: stoppedMatchNotice(event.stopReason) }))
        })
      })
    } catch (value) {
      if (controller.signal.aborted) return
      const fallback = value instanceof ApiError
        ? value.message === value.code ? 'Unable to run match work. Use Refresh to check the batch status.' : `${value.message.replace(/[.!?]+$/, '')}.`
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
    if (cancelPending || needsRefresh) return
    setCancelPending(true)
    try { await matchApi.cancel(batch.activeOperation!.operationId); if (alive.current) preserveReadingPosition(() => setBatchNotices(current => ({ ...current, [batch.batchId]: { text: 'Cancellation requested. Waiting for active work to stop.', tone: 'status' } }))) }
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
          {attempt ? <ImportAttemptDetails attempt={attempt} /> : <div className="import-details"><h2 className="history-card-heading">Completed import</h2><p>Refresh import history to load this import's details.</p></div>}
          {batch ? <div className="batch-summary">
            <h2 className="history-card-heading" id={`batch-heading-${id}`}>Batch {number} · {label(batch.status)}</h2>
            <p><strong>{batch.completedPairCount}/{batch.totalPairCount} completed</strong> · {batch.failedPairCount} failed · {batch.unfinishedPairCount} unfinished · {batch.activePairCount} active</p>
            <div className="actions">
              {user.role === 'Operator' && !needsRefresh && [['start', 'Start match'], ['continue', 'Continue unfinished'], ['retryFailed', 'Retry failed']].map(([action, text]) =>
                batch.availableActions.includes(action) && <button key={action} disabled={pending} onClick={() => operate(batch, action)}>{text}</button>)}
              {batch.activeOperation?.canCancel && user.role === 'Operator' && !needsRefresh && <button disabled={cancelPending} onClick={() => cancel(batch)}>{cancelPending ? 'Requesting cancellation…' : 'Cancel match'}</button>}
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
          <td>{(pair.state === 'completed' || pair.latestAttempt) && <button id={`inspection-toggle-${pairKey(pair)}`} className="quiet-button" aria-expanded={expandedPair === pairKey(pair)} aria-controls={`inspection-${pairKey(pair)}`} onClick={() => openPair(pair)}>{expandedPair === pairKey(pair) ? 'Close details' : 'Inspect'} {pair.patientId} {pair.nctId}</button>}</td></tr>
          {expandedPair === pairKey(pair) && <tr className="assessment-row"><td colSpan={4}><div id={`inspection-${pairKey(pair)}`}>
            {inspectionError && <p role="alert" className="match-message inspection-message">{detail && 'Showing previously loaded details. '}{inspectionError}</p>}
            {detail && pairKey(detail) === pairKey(pair) ? <Assessment detail={detail} headingLevel={4} />
              : !inspectionError && <p role="status" className="inspection-message">Loading assessment…</p>}
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
