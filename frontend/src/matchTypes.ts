export interface MatchCounts {
  totalPairCount: number; completedPairCount: number; failedPairCount: number; unfinishedPairCount: number; activePairCount: number
}
export interface MatchOperation { operationId: string; action: string; startedAt: string; selectedPairCount: number; canCancel: boolean }
export interface StructuredFinding { result: string; explanation: string; patientEvidence: Record<string, unknown>; studyEvidence: Record<string, unknown> }
export interface StructuredChecks { age: StructuredFinding; sex: StructuredFinding }
export interface MatchPair {
  batchId: string; patientId: string; nctId: string; state: string; pairSummary: string | null; structuredChecks: StructuredChecks | null
  latestAttempt: { status: string; failureCode: string | null; failureExplanation: string | null } | null
}
export interface MatchBatch extends MatchCounts {
  batchId: string; version: string; status: string; activeOperation: MatchOperation | null; availableActions: string[]; pairs: MatchPair[] | null
}
export interface MatchState { configurationError: string | null; activeOperation: MatchOperation | null; batches: MatchBatch[] }
export interface Evidence { sourceId: string; value?: unknown; quote?: string }
export interface LabelFinding { result: string; explanation: string; patientEvidence: Evidence[]; studyEvidence: Evidence[]; examinedSources: string[] }
export interface ConditionLabel {
  conditionLabelId: string; result: string; requirementSummary: string; explanation: string
  cancerType: LabelFinding; stage: LabelFinding; diseaseState: LabelFinding
  unassessedQualifiers: { qualifier: string; reason: string; studyEvidence: Evidence[] }[]
}
export interface PairDetails extends MatchPair {
  conditionLabels: ConditionLabel[] | null; skipReason: string | null; assessmentLimits: string
  inputSnapshot: { patient: { ageYears: number | null; sex: string; cancerType: string | null; stage: { status: string; value: string | null; classificationSystem: string | null }; diseaseState: { status: string; value: string | null } }; study: { conditions: string[] | null; minimumAge: string | null; maximumAge: string | null; sex: string | null } }
}
export interface MatchEvent extends MatchCounts {
  type: string; operationId: string; batchId: string; sequence: number; serverTime: string; version: string; status: string
  patientId: string | null; nctId: string | null; pairSummary: string | null; structuredChecks: StructuredChecks | null
  failureCode: string | null; failureExplanation: string | null; stopReason: string | null
}
