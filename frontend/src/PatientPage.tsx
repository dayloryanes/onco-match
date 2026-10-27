import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from './api'
import type { PatientDetails, PatientSummary } from './api'

type LoadState = { status: 'loading' } | { status: 'error' } | { status: 'notFound' } |
  { status: 'ready'; data: PatientSummary[] | PatientDetails }

function label(value: string | null) {
  const labels: Record<string, string> = {
    female: 'Female', male: 'Male', unknown: 'Unknown',
    newlyDiagnosed: 'Newly diagnosed', recurrent: 'Recurrent', refractory: 'Refractory',
  }
  return value === null ? 'Unknown' : labels[value] ?? value
}

function fact(status: string, value: string | null) {
  return status === 'notApplicable' ? 'Not applicable' : status === 'unknown' ? 'Unknown' : label(value)
}

export default function PatientPage({ onExpired }: { onExpired: () => void }) {
  const { id } = useParams()
  const [state, setState] = useState<LoadState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

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

  let content
  if (state.status === 'loading') content = <p role="status">Loading patients…</p>
  else if (state.status === 'error') content = <div role="alert"><p>Unable to load patient information.</p><button onClick={() => { setState({ status: 'loading' }); setAttempt(value => value + 1) }}>Retry</button></div>
  else if (state.status === 'notFound') content = <h1>Patient not found</h1>
  else if (Array.isArray(state.data)) content = <>
    <p className="eyebrow">Patient directory</p><h1>Patients</h1>
    <p>{state.data.length} fictional patient records</p>
    <div className="table-scroll"><table>
      <thead><tr><th scope="col">Patient</th><th scope="col">Age</th><th scope="col">Sex</th><th scope="col">Cancer type</th></tr></thead>
      <tbody>{state.data.map(patient => <tr key={patient.id}>
        <th scope="row"><Link to={`/patients/${encodeURIComponent(patient.id)}`}>{patient.id}</Link></th>
        <td>{patient.ageYears === null ? 'Unknown' : `${patient.ageYears} years`}</td>
        <td>{label(patient.sex)}</td><td>{label(patient.cancerType)}</td>
      </tr>)}</tbody>
    </table></div>
  </>
  else {
    const patient = state.data
    content = <><p className="eyebrow">Patient details</p><h1>Patient {patient.id}</h1>
      <dl className="patient-facts">
        <div><dt>Age</dt><dd>{patient.ageYears === null ? 'Unknown' : `${patient.ageYears} years`}</dd></div>
        <div><dt>Sex</dt><dd>{label(patient.sex)}</dd></div>
        <div><dt>Cancer type</dt><dd>{label(patient.cancerType)}</dd></div>
        <div><dt>Stage at diagnosis</dt><dd>{fact(patient.stage.status, patient.stage.value)}</dd></div>
        <div><dt>Classification system</dt><dd>{patient.stage.status === 'notApplicable' ? 'Not applicable' : patient.stage.classificationSystem ?? (patient.stage.status === 'known' ? 'System unknown' : 'Unknown')}</dd></div>
        <div><dt>Disease state</dt><dd>{fact(patient.diseaseState.status, patient.diseaseState.value)}</dd></div>
      </dl>
    </>
  }

  return <section>
    <nav aria-label="Patient navigation"><Link to="/">Dashboard</Link>{id && <Link to="/patients">All patients</Link>}</nav>
    <p className="notice">Fictional patient data for demonstration only.</p>
    {content}
  </section>
}
