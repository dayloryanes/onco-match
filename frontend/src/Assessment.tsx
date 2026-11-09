import type { Evidence, LabelFinding, PairDetails } from './matchTypes'
import { label } from './matchDisplay'
import { patientSex, patientStage, classificationSystem, diseaseState } from './patientDisplay'
import { studySex } from './studyDisplay'

const display = (value: unknown) => value === null || value === undefined ? 'Unknown' : typeof value === 'string' ? value : JSON.stringify(value)

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
function Finding({ title, finding, heading: Heading }: { title: string; finding: LabelFinding; heading: 'h5' | 'h6' }) {
  return <div className="assessment-stack"><Heading className="assessment-subheading">{title}: <span className="finding-result">{label(finding.result)}</span></Heading><p>{finding.explanation}</p>
    <EvidenceList evidence={finding.patientEvidence} /><EvidenceList evidence={finding.studyEvidence} /></div>
}
export default function Assessment({ detail, headingLevel }: { detail: PairDetails; headingLevel: 3 | 4 }) {
  const snapshot = detail.inputSnapshot
  const Heading = headingLevel === 3 ? 'h3' : 'h4'
  const SectionHeading = headingLevel === 3 ? 'h4' : 'h5'
  const FindingHeading = headingLevel === 3 ? 'h5' : 'h6'
  return <section className="assessment-detail" aria-label={`Assessment ${detail.patientId} ${detail.nctId}`}>
    <div className="assessment-content">
    <header className="assessment-stack">
    <Heading className="assessment-heading assessment-title">Assessment: {detail.patientId} · {detail.nctId}</Heading>
    <p>{label(detail.pairSummary ?? detail.state)}</p>
    {detail.skipReason && <p>{detail.skipReason}</p>}
    </header>
    {detail.structuredChecks && <section className="assessment-major assessment-groups" aria-label="Age and sex checks">
      <div className="assessment-stack"><SectionHeading className="assessment-subheading">Age check: <span className="finding-result">{label(detail.structuredChecks.age.result)}</span></SectionHeading><p>{detail.structuredChecks.age.explanation}</p></div>
      <div className="assessment-stack"><SectionHeading className="assessment-subheading">Sex check: <span className="finding-result">{label(detail.structuredChecks.sex.result)}</span></SectionHeading><p>{detail.structuredChecks.sex.explanation}</p></div>
    </section>}
    {detail.conditionLabels?.map(condition => {
      const index = conditionIndex(condition.conditionLabelId)
      const text = index === null ? null : snapshot?.study.conditions?.[index]
      const examinedSources = [...new Set([condition.cancerType, condition.stage, condition.diseaseState].flatMap(finding => finding.examinedSources))]
      return <article key={condition.conditionLabelId} className="condition-assessment assessment-major assessment-groups">
      <header className="assessment-stack"><SectionHeading className="assessment-title">{index === null ? condition.conditionLabelId : `Condition ${index + 1}`} · {text?.trim() ? text : 'Condition text not available'}</SectionHeading><p>{label(condition.result)}</p></header>
      <div className="assessment-stack"><FindingHeading className="assessment-subheading">Requirement summary</FindingHeading><p>{condition.requirementSummary}</p></div>
      <div className="assessment-stack"><FindingHeading className="assessment-subheading">Explanation</FindingHeading><p>{condition.explanation}</p></div>
      <Finding title="Cancer type" finding={condition.cancerType} heading={FindingHeading} /><Finding title="Stage" finding={condition.stage} heading={FindingHeading} /><Finding title="Disease state" finding={condition.diseaseState} heading={FindingHeading} />
      <section className="assessment-stack"><FindingHeading className="assessment-subheading">Unassessed qualifiers</FindingHeading>{condition.unassessedQualifiers.length === 0 ? <p>None stated.</p> : condition.unassessedQualifiers.map((qualifier, i) => <div key={i} className="assessment-stack">
        <p><span className="fact-label">{qualifier.qualifier}</span>: {qualifier.reason}</p><EvidenceList evidence={qualifier.studyEvidence} /></div>)}</section>
      {examinedSources.length > 0 && <p className="attempt-id">Examined: {examinedSources.map(sourceLabel).join(', ')}</p>}
    </article>})}
    </div>
    {snapshot && <details className="assessment-major"><summary>Exact input snapshot</summary>
    <section className="snapshot-section" aria-label="Patient snapshot"><SectionHeading className="assessment-subheading">Patient</SectionHeading><dl className="patient-facts">
      <div><dt>Age (completed years)</dt><dd>{display(snapshot.patient.ageYears)}</dd></div>
      <div><dt>Sex</dt><dd>{patientSex(snapshot.patient.sex)}</dd></div><div><dt>Cancer type</dt><dd>{display(snapshot.patient.cancerType)}</dd></div>
      <div><dt>Stage at diagnosis</dt><dd>{patientStage(snapshot.patient.stage)}</dd></div>
      <div><dt>Classification system</dt><dd>{classificationSystem(snapshot.patient.stage)}</dd></div>
      <div><dt>Disease state</dt><dd>{diseaseState(snapshot.patient.diseaseState)}</dd></div>
    </dl></section>
    <section className="snapshot-section" aria-label="Study snapshot"><SectionHeading className="assessment-subheading">Study</SectionHeading><dl className="patient-facts">
      <div><dt>Age bounds</dt><dd>{display(snapshot.study.minimumAge)} — {display(snapshot.study.maximumAge)}</dd></div>
      <div><dt>Sex</dt><dd>{display(studySex(snapshot.study.sex))}</dd></div><div><dt>Conditions</dt><dd>{snapshot.study.conditions?.length ? <ul>{snapshot.study.conditions.map((condition, i) => <li key={i}>{condition.trim() ? condition : 'Not available'}</li>)}</ul> : 'Not available'}</dd></div>
    </dl></section></details>}
    {!!detail.attemptHistory?.length && <details className="assessment-major"><summary>Attempt history</summary>
      <ol className="attempt-history">{detail.attemptHistory.map((attempt, index) => <li key={attempt.id} className="snapshot-section">
        <SectionHeading className="assessment-subheading">Attempt {index + 1}: {label(attempt.status)}{attempt.status === 'interrupted' && attempt.failureExplanation && <> · {label(attempt.failureExplanation)}</>}</SectionHeading>
        <dl className="patient-facts">
          <div><dt>Started</dt><dd>{new Date(attempt.startedAt).toLocaleString()}</dd></div>
          <div><dt>Finished</dt><dd>{attempt.finishedAt ? new Date(attempt.finishedAt).toLocaleString() : 'In progress'}</dd></div>
          {attempt.elapsedMilliseconds != null && <div><dt>Duration</dt><dd>{(attempt.elapsedMilliseconds / 1000).toFixed(2)} seconds</dd></div>}
          {attempt.route && <div><dt>Route</dt><dd>{attempt.route}</dd></div>}
          {attempt.model && <div><dt>Model</dt><dd>{attempt.model}</dd></div>}
          {attempt.requestTimeoutSeconds != null && <div><dt>Request timeout</dt><dd>{attempt.requestTimeoutSeconds} seconds</dd></div>}
          {attempt.status !== 'interrupted' && attempt.failureExplanation && <div><dt>Failure reason</dt><dd>{label(attempt.failureExplanation)}</dd></div>}
          {attempt.failureCode && <div><dt>Failure code</dt><dd>{attempt.failureCode}</dd></div>}
          <div><dt>Attempt ID</dt><dd className="attempt-id">{attempt.id}</dd></div>
          <div><dt>Operation ID</dt><dd className="attempt-id">{attempt.operationId}</dd></div>
        </dl>
      </li>)}</ol>
    </details>}
  </section>
}
