import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from './api'
import type { StudyDetails, StudySummary } from './api'

type LoadState = { status: 'loading' | 'error' | 'notFound' } | { status: 'ready'; data: StudySummary[] | StudyDetails }
function display(value: string | null) { return value?.trim() ? value : 'Not available' }
function Conditions({ values }: { values: string[] | null }) {
  return values?.length ? <ul>{values.map((value, index) => <li key={index}>{display(value)}</li>)}</ul> : <>Not available</>
}

export default function StudyPage({ onExpired }: { onExpired: () => void }) {
  const { nctId } = useParams()
  const [state, setState] = useState<LoadState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    const load = nctId ? api.study(nctId, controller.signal) : api.studies(controller.signal)
    load.then(data => { if (!controller.signal.aborted) setState({ status: 'ready', data }) }).catch(failure => {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else setState({ status: failure instanceof ApiError && failure.status === 404 ? 'notFound' : 'error' })
    })
    return () => controller.abort()
  }, [nctId, attempt, onExpired])

  function reload() { setState({ status: 'loading' }); setAttempt(value => value + 1) }
  let content
  if (state.status === 'loading') content = <p role="status">Loading studies…</p>
  else if (state.status === 'error') content = <div role="alert"><p>Unable to load study information.</p><button onClick={reload}>Retry</button></div>
  else if (state.status === 'notFound') content = <h1>Study not found</h1>
  else if (state.status === 'ready' && Array.isArray(state.data)) content = <>
    <h1>Studies</h1><p>{state.data.length} imported studies, in first-import order</p><button onClick={reload}>Refresh</button>
    {state.data.length === 0 ? <p>No studies imported yet.</p> : <div className="table-scroll"><table>
      <thead><tr><th scope="col">Study</th><th scope="col">Title</th><th scope="col">Recruitment status</th><th scope="col">Study type</th><th scope="col">Conditions</th></tr></thead>
      <tbody>{state.data.map(study => <tr key={study.nctId}>
        <th scope="row"><Link to={`/studies/${encodeURIComponent(study.nctId)}`}>{study.nctId}</Link></th>
        <td>{display(study.title)}</td><td>{display(study.recruitmentStatus)}</td><td>{display(study.studyType)}</td><td><Conditions values={study.conditions} /></td>
      </tr>)}</tbody>
    </table></div>}
  </>
  else if (state.status === 'ready' && !Array.isArray(state.data)) {
    const study = state.data
    content = <><h1>Study {study.nctId}</h1><dl className="patient-facts study-facts">
      <div><dt>Title</dt><dd>{display(study.title)}</dd></div>
      <div><dt>Recruitment status</dt><dd>{display(study.recruitmentStatus)}</dd></div>
      <div><dt>Study type</dt><dd>{display(study.studyType)}</dd></div>
      <div><dt>Conditions</dt><dd><Conditions values={study.conditions} /></dd></div>
      <div><dt>Brief summary</dt><dd>{display(study.briefSummary)}</dd></div>
      <div><dt>Minimum age</dt><dd>{display(study.minimumAge)}</dd></div>
      <div><dt>Maximum age</dt><dd>{display(study.maximumAge)}</dd></div>
      <div><dt>Sex</dt><dd>{display(study.sex)}</dd></div>
    </dl><a href={study.registryUrl} target="_blank" rel="noreferrer">View on ClinicalTrials.gov</a></>
  }
  return <section>
    {nctId && <Link to="/studies">All studies</Link>}
    <p className="notice">Registry source information. Missing fields do not establish eligibility. No patient screening is performed during import.</p>
    {content}
  </section>
}
