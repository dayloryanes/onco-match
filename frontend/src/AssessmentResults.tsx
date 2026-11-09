import { Fragment, useEffect, useRef, useState } from 'react'
import { ApiError, matchApi } from './api'
import Assessment from './Assessment'
import { label, limits } from './matchDisplay'
import type { MatchPair, PairDetails } from './matchTypes'
import type { PreserveReadingPosition } from './useReadingPosition'

export default function AssessmentResults({ filter, id, onExpired, preserveReadingPosition }: {
  filter: 'patientId' | 'nctId'; id: string; onExpired: () => void; preserveReadingPosition: PreserveReadingPosition
}) {
  const [pairs, setPairs] = useState<MatchPair[] | null>(null)
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const [refresh, setRefresh] = useState(0)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [detail, setDetail] = useState<PairDetails | null>(null)
  const [detailError, setDetailError] = useState('')
  const detailRequest = useRef<AbortController | null>(null)
  const key = (pair: MatchPair) => `${pair.patientId}-${pair.nctId}`

  useEffect(() => {
    const controller = new AbortController()
    matchApi.pairs(filter, id, controller.signal).then(data => {
      if (!controller.signal.aborted) preserveReadingPosition(() => { setPairs(data); setError(''); setPending(false) })
    }).catch(failure => {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else preserveReadingPosition(() => { setError('Unable to load assessments.'); setPending(false) })
    })
    return () => controller.abort()
  }, [filter, id, refresh, onExpired, preserveReadingPosition])

  useEffect(() => () => detailRequest.current?.abort(), [])

  function close(pair: MatchPair) {
    detailRequest.current?.abort()
    setExpanded(null); setDetail(null); setDetailError('')
    document.getElementById(`result-toggle-${key(pair)}`)?.focus()
  }

  function inspect(pair: MatchPair) {
    if (expanded === key(pair)) { close(pair); return }
    detailRequest.current?.abort()
    setExpanded(key(pair)); setDetail(null); setDetailError('')
  }

  const inspectedPair = pairs?.find(pair => key(pair) === expanded)
  useEffect(() => {
    if (!inspectedPair) return
    const controller = new AbortController(); detailRequest.current = controller
    matchApi.pair(inspectedPair.patientId, inspectedPair.nctId, controller.signal).then(data => {
      if (!controller.signal.aborted) preserveReadingPosition(() => { setDetail(data); setDetailError('') })
    }).catch(failure => {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else preserveReadingPosition(() => setDetailError('Unable to load assessment details. Close and reopen to retry.'))
    })
    return () => controller.abort()
  }, [inspectedPair, onExpired, preserveReadingPosition])

  function reload() {
    setPending(true)
    setRefresh(value => value + 1)
  }

  return <section className="batch-results" aria-label="Stored assessments">
    <h3>Assessments</h3>
    <p className="notice">{limits}</p>
    {error && <p role="alert">{error}{pairs && ' Previously loaded results are still shown.'}</p>}
    <button className="quiet-button" disabled={pending} onClick={reload}>{error ? 'Retry assessments' : 'Refresh assessments'}</button>
    {!pairs && !error && <p role="status">Loading assessments…</p>}
    {pairs?.length === 0 && <p>No assessments available yet.</p>}
    {!!pairs?.length && <div className="table-scroll"><table>
      <thead><tr><th scope="col">{filter === 'patientId' ? 'Study' : 'Patient'}</th><th scope="col">Assessment</th><th scope="col">Details</th></tr></thead>
      <tbody>{pairs.map(pair => <Fragment key={key(pair)}>
        <tr><th scope="row">{filter === 'patientId' ? pair.nctId : pair.patientId}</th>
          <td>{label(pair.pairSummary ?? pair.state)}{pair.latestAttempt?.failureExplanation && <> · {label(pair.latestAttempt.failureExplanation)}</>}</td>
          <td>{(pair.state === 'completed' || pair.latestAttempt) && <button id={`result-toggle-${key(pair)}`} className="quiet-button inspection-toggle"
            aria-label={`${expanded === key(pair) ? 'Close details' : 'Inspect'} ${pair.patientId} ${pair.nctId}`}
            aria-expanded={expanded === key(pair)} aria-controls={`result-detail-${key(pair)}`} onClick={() => inspect(pair)}>
            {expanded === key(pair) ? 'Close details' : 'Inspect'}</button>}</td></tr>
        {expanded === key(pair) && <tr className="assessment-row"><td colSpan={3}><div id={`result-detail-${key(pair)}`}>
          {detailError && <p role="alert">{detail && 'Showing previously loaded details. '}{detailError}</p>}
          {detail ? <Assessment detail={detail} /> : !detailError && <p role="status">Loading assessment…</p>}
          <div className="actions assessment-actions"><button className="quiet-button" onClick={() => close(pair)}>Close assessment</button></div>
        </div></td></tr>}
      </Fragment>)}</tbody>
    </table></div>}
  </section>
}
