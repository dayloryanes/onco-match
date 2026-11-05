import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from './api'
import type { PatientDetails, PatientSummary } from './api'
import { useListScroll } from './listNavigation'
import type { ListNavigation } from './listNavigation'
import { patientSex, patientStage, classificationSystem, diseaseState } from './patientDisplay'

type LoadState = { status: 'loading' } | { status: 'error' } | { status: 'notFound' } |
  { status: 'ready'; data: PatientSummary[] | PatientDetails }

export default function PatientPage({ onExpired, navigation, isVisible }: { onExpired: () => void; navigation: ListNavigation; isVisible: boolean }) {
  const { id } = useParams()
  const [state, setState] = useState<LoadState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  useListScroll(navigation, !id, state.status === 'ready' && isVisible)

  useEffect(() => {
    const controller = new AbortController()
    const load = id ? api.patient(id, controller.signal) : api.patients(controller.signal)
    load.then(data => {
      if (!controller.signal.aborted) setState({ status: 'ready', data })
    }).catch(failure => {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else setState({ status: failure instanceof ApiError && failure.status === 404 ? 'notFound' : 'error' })
    })
    return () => controller.abort()
  }, [id, attempt, onExpired])

  const notice = <p className="notice">Fictional patient data for demonstration only.</p>
  let content
  if (state.status === 'loading') content = <p role="status">Loading patients…</p>
  else if (state.status === 'error') content = <div role="alert"><p>Unable to load patient information.</p><button onClick={() => { setState({ status: 'loading' }); setAttempt(value => value + 1) }}>Retry</button></div>
  else if (state.status === 'notFound') content = <h1>Patient not found</h1>
  else if (Array.isArray(state.data)) content = <>
    <p className="eyebrow">Patient directory</p><h1>Patients</h1>
    <p>{state.data.length} fictional {state.data.length === 1 ? 'patient' : 'patients'}</p>
    {notice}
    <div className="table-scroll"><table>
      <thead><tr><th scope="col">Patient</th><th scope="col">Age</th><th scope="col">Sex</th><th scope="col">Cancer type</th></tr></thead>
      <tbody>{state.data.map(patient => <tr key={patient.id} className={navigation.lastViewedId === patient.id ? 'last-viewed' : undefined}>
        <th scope="row"><Link to={`/patients/${encodeURIComponent(patient.id)}`} onClick={event => navigation.remember(patient.id, event)}>{patient.id}</Link>
          {navigation.lastViewedId === patient.id && <span className="last-viewed-label">Last viewed</span>}</th>
        <td>{patient.ageYears === null ? 'Unknown' : `${patient.ageYears} years`}</td>
        <td>{patientSex(patient.sex)}</td><td>{patient.cancerType ?? 'Unknown'}</td>
      </tr>)}</tbody>
    </table></div>
  </>
  else {
    const patient = state.data
    content = <><p className="eyebrow">Patient details</p><h1>Patient {patient.id}</h1>
      {notice}
      <dl className="patient-facts">
        <div><dt>Age</dt><dd>{patient.ageYears === null ? 'Unknown' : `${patient.ageYears} years`}</dd></div>
        <div><dt>Sex</dt><dd>{patientSex(patient.sex)}</dd></div>
        <div><dt>Cancer type</dt><dd>{patient.cancerType ?? 'Unknown'}</dd></div>
        <div><dt>Stage at diagnosis</dt><dd>{patientStage(patient.stage)}</dd></div>
        <div><dt>Classification system</dt><dd>{classificationSystem(patient.stage)}</dd></div>
        <div><dt>Disease state</dt><dd>{diseaseState(patient.diseaseState)}</dd></div>
      </dl>
    </>
  }

  return <section>
    {id && <nav aria-label="Patient navigation"><Link to="/patients">All patients</Link></nav>}
    {content}
    {state.status !== 'ready' && notice}
  </section>
}
