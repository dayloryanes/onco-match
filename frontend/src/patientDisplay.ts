type Fact = { status: string; value: string | null }
type Stage = Fact & { classificationSystem: string | null }

export function patientSex(value: string) {
  const labels: Record<string, string> = { female: 'Female', male: 'Male', unknown: 'Unknown' }
  return labels[value] ?? value
}

export function patientStage(stage: Fact) {
  return stage.status === 'notApplicable' ? 'Not applicable' : stage.status === 'unknown' ? 'Unknown' : stage.value ?? 'Unknown'
}

export function classificationSystem(stage: Stage) {
  return stage.status === 'notApplicable' ? 'Not applicable' : stage.classificationSystem ?? (stage.status === 'known' ? 'System unknown' : 'Unknown')
}

export function diseaseState(fact: Fact) {
  const labels: Record<string, string> = { newlyDiagnosed: 'Newly diagnosed', recurrent: 'Recurrent', refractory: 'Refractory' }
  const value = patientStage(fact)
  return labels[value] ?? value
}
