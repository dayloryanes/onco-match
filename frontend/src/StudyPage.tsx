import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { ClipboardEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from './api'
import type { StudyDetails, StudySummary } from './api'
import { useListScroll } from './listNavigation'
import type { ListNavigation } from './listNavigation'
import { studySex } from './studyDisplay'
import type { PreserveReadingPosition } from './useReadingPosition'
import AssessmentResults from './AssessmentResults'

type LoadState = { status: 'loading' | 'error' | 'notFound' } | { status: 'ready'; data: StudySummary[] | StudyDetails }
function display(value: string | null) { return value?.trim() ? value : 'Not available' }
function studyCode(value: string | null) {
  const labels: Record<string, string> = {
    RECRUITING: 'Recruiting', NOT_YET_RECRUITING: 'Not yet recruiting',
    INTERVENTIONAL: 'Interventional', OBSERVATIONAL: 'Observational',
  }
  return value === null ? display(value) : labels[value] ?? display(value)
}
function ConditionWords({ value }: { value: string }) {
  const container = useRef<HTMLSpanElement>(null)
  useLayoutEffect(() => {
    const element = container.current!
    let active = true
    const measure = () => {
      if (!active || element.clientWidth === 0) return
      const context = document.createElement('canvas').getContext('2d')
      if (!context) return
      context.font = getComputedStyle(element).font
      const available = element.getBoundingClientRect().width
      for (const child of element.children) {
        const word = child as HTMLElement
        // Measure without temporarily widening an auto-sized table column.
        const overlong = context.measureText(word.getAttribute('aria-label') ?? '').width > available
        // A split word must flow inline so the following word can share its
        // final line. Words that fit remain whole-word boxes.
        word.style.display = overlong ? 'inline' : 'inline-block'
      }
    }
    measure()
    let width = element.clientWidth
    let frame = 0
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(entries => {
      const next = entries[0].contentRect.width
      if (Math.abs(next - width) > .5) {
        width = next
        cancelAnimationFrame(frame)
        frame = requestAnimationFrame(measure)
      }
    })
    observer?.observe(element)
    void document.fonts?.ready.then(measure)
    return () => { active = false; cancelAnimationFrame(frame); observer?.disconnect() }
  }, [value])
  return <span className="condition-text" ref={container}>{value.split(/(\s+)/).map((word, index) => /^\s*$/.test(word) ? word :
    <span className="condition-word" aria-label={word} key={index}>{word.replace(/(?<=[\p{L}\p{N}])(?=[\p{L}\p{N}])/gu, '\u00ad')}</span>)}</span>
}
function copyStudyText(event: ClipboardEvent<HTMLDivElement>) {
  const selected = window.getSelection()?.toString()
  if (selected?.includes('\u00ad')) {
    event.clipboardData.setData('text/plain', selected.replaceAll('\u00ad', ''))
    event.preventDefault()
  }
}
function Conditions({ values, wrapWords = false }: { values: string[] | null; wrapWords?: boolean }) {
  return values?.length ? <ul>{values.map((value, index) => <li key={index}>{wrapWords ? <ConditionWords value={display(value)} /> : display(value)}</li>)}</ul> : <>Not available</>
}

export default function StudyPage({ onExpired, navigation, isVisible, preserveReadingPosition }: { onExpired: () => void; navigation: ListNavigation; isVisible: boolean; preserveReadingPosition: PreserveReadingPosition }) {
  const { nctId } = useParams()
  const [state, setState] = useState<LoadState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [refreshPending, setRefreshPending] = useState(false)
  const [refreshError, setRefreshError] = useState(false)
  const refreshController = useRef<AbortController | null>(null)
  useListScroll(navigation, !nctId, state.status === 'ready' && isVisible)
  useEffect(() => {
    const controller = new AbortController()
    const load = nctId ? api.study(nctId, controller.signal) : api.studies(controller.signal)
    load.then(data => { if (!controller.signal.aborted) setState({ status: 'ready', data }) }).catch(failure => {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else setState({ status: failure instanceof ApiError && failure.status === 404 ? 'notFound' : 'error' })
    })
    return () => { controller.abort(); refreshController.current?.abort() }
  }, [nctId, attempt, onExpired])

  async function reload() {
    if (state.status !== 'ready') { setState({ status: 'loading' }); setAttempt(value => value + 1); return }
    if (nctId || refreshController.current) return
    const controller = new AbortController(); refreshController.current = controller
    setRefreshPending(true)
    preserveReadingPosition(() => setRefreshError(false))
    try {
      const data = await api.studies(controller.signal)
      if (!controller.signal.aborted) preserveReadingPosition(() => setState({ status: 'ready', data }))
    } catch (failure) {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else preserveReadingPosition(() => setRefreshError(true))
    } finally {
      if (!controller.signal.aborted) setRefreshPending(false)
      if (refreshController.current === controller) refreshController.current = null
    }
  }
  const notice = <p className="notice">Registry source information. Missing fields do not establish eligibility. No patient screening is performed during import.</p>
  let content
  if (state.status === 'loading') content = <p role="status">Loading studies…</p>
  else if (state.status === 'error') content = <div role="alert"><p>Unable to load study information.</p><button onClick={reload}>Retry</button></div>
  else if (state.status === 'notFound') content = <h1>Study not found</h1>
  else if (state.status === 'ready' && Array.isArray(state.data)) content = <>
    <p className="eyebrow">Study directory</p><h1>Studies</h1>
    <p>{state.data.length} imported {state.data.length === 1 ? 'study' : 'studies'} · Listed in import order</p>
    {notice}
    {refreshError && <p role="alert">Unable to refresh studies. Existing studies are still shown. Use Refresh to retry.</p>}
    <button className="quiet-button" onClick={reload} disabled={refreshPending}>Refresh</button>
    {state.data.length === 0 ? <p>No studies imported yet.</p> : <div className="table-scroll" onCopy={copyStudyText}><table>
      <thead><tr><th scope="col">Study</th><th scope="col">Title</th><th scope="col">Recruitment status</th><th scope="col">Study type</th><th scope="col">Conditions</th></tr></thead>
      <tbody>{state.data.map(study => <tr key={study.nctId} className={navigation.lastViewedId === study.nctId ? 'last-viewed' : undefined}>
        <th scope="row"><Link to={`/studies/${encodeURIComponent(study.nctId)}`} onClick={event => navigation.remember(study.nctId, event)}>{study.nctId}</Link>
          {navigation.lastViewedId === study.nctId && <span className="last-viewed-label">Last viewed</span>}</th>
        <td className="study-title">{display(study.title)}</td><td className="study-status">{studyCode(study.recruitmentStatus)}</td><td className="study-type">{studyCode(study.studyType)}</td><td className="study-conditions"><Conditions values={study.conditions} wrapWords /></td>
      </tr>)}</tbody>
    </table></div>}
  </>
  else if (state.status === 'ready' && !Array.isArray(state.data)) {
    const study = state.data
    content = <><p className="eyebrow">Study details</p><h1>Study {study.nctId}</h1>
    {notice}
    <dl className="patient-facts study-facts">
      <div><dt>Title</dt><dd>{display(study.title)}</dd></div>
      <div><dt>Recruitment status</dt><dd>{studyCode(study.recruitmentStatus)}</dd></div>
      <div><dt>Study type</dt><dd>{studyCode(study.studyType)}</dd></div>
      <div><dt>Conditions</dt><dd><Conditions values={study.conditions} /></dd></div>
      <div><dt>Brief summary</dt><dd>{display(study.briefSummary)}</dd></div>
      <div><dt>Minimum age</dt><dd>{display(study.minimumAge)}</dd></div>
      <div><dt>Maximum age</dt><dd>{display(study.maximumAge)}</dd></div>
      <div><dt>Sex</dt><dd>{display(studySex(study.sex))}</dd></div>
    </dl><a href={study.registryUrl} target="_blank" rel="noreferrer">View on ClinicalTrials.gov</a>
    <AssessmentResults key={study.nctId} filter="nctId" id={study.nctId} onExpired={onExpired} preserveReadingPosition={preserveReadingPosition} /></>
  }
  return <section>
    {nctId && <nav aria-label="Study navigation"><Link to="/studies">All studies</Link></nav>}
    {content}
    {state.status !== 'ready' && notice}
  </section>
}
