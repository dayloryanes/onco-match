import type { MatchEvent } from './matchTypes'

const types = new Set(['operationStarted', 'pairStarted', 'pairCompleted', 'pairFailed', 'operationFinished', 'operationStopped', 'heartbeat'])
const statuses = new Set(['noWork', 'notStarted', 'running', 'stopping', 'unfinished', 'finishedWithFailures', 'completed'])
const summaries = new Set(['excludedByAgeSex', 'conditionLabelAlignmentSupported', 'mixedConditionLabelFindings', 'conditionLabelConflictsIdentified', 'conditionLabelAlignmentUndetermined'])
const stopReasons = new Set(['cancelled', 'sessionExpired', 'disconnected', 'logout', 'internalError', 'inferenceUnavailable'])
const findingResults = new Set(['satisfied', 'excluded', 'undetermined', 'noRestrictionIdentified'])
function checks(value: unknown): boolean {
  if (!value || typeof value !== 'object') return false
  return ['age', 'sex'].every(key => {
    const finding = (value as Record<string, unknown>)[key]
    if (!finding || typeof finding !== 'object') return false
    const fields = finding as Record<string, unknown>
    return typeof fields.result === 'string' && findingResults.has(fields.result) && typeof fields.explanation === 'string'
      && !!fields.patientEvidence && typeof fields.patientEvidence === 'object' && !!fields.studyEvidence && typeof fields.studyEvidence === 'object'
  })
}

export async function readMatchStream(response: Response, batchId: string, receive: (event: MatchEvent) => void) {
  if (response.headers.get('Content-Type')?.split(';')[0] !== 'application/x-ndjson' || !response.body) throw new Error('Missing progress stream')
  const reader = response.body.getReader()
  const decoder = new TextDecoder('utf-8', { fatal: true })
  let buffer = '', operationId = '', sequence = 0, terminal = false
  function line(text: string) {
    const event = JSON.parse(text) as MatchEvent
    const counts = [event.completedPairCount, event.failedPairCount, event.unfinishedPairCount, event.activePairCount]
    if (!types.has(event.type) || !statuses.has(event.status) || terminal || event.batchId !== batchId || typeof event.operationId !== 'string' || !event.operationId
      || (sequence === 0 ? event.type !== 'operationStarted' : event.operationId !== operationId)
      || event.sequence !== sequence + 1 || typeof event.version !== 'string' || !event.version
      || typeof event.serverTime !== 'string' || !Number.isFinite(Date.parse(event.serverTime))
      || !Number.isSafeInteger(event.totalPairCount) || event.totalPairCount < 0
      || counts.some(value => !Number.isSafeInteger(value) || value < 0) || event.activePairCount > 1
      || counts.reduce((sum, value) => sum + value, 0) !== event.totalPairCount
      || (event.pairSummary != null && (typeof event.pairSummary !== 'string' || !summaries.has(event.pairSummary)))
      || (event.structuredChecks != null && !checks(event.structuredChecks))
      || (event.failureCode != null && typeof event.failureCode !== 'string')
      || (event.failureExplanation != null && typeof event.failureExplanation !== 'string')
      || (event.stopReason != null && (typeof event.stopReason !== 'string' || !stopReasons.has(event.stopReason)))
      || (event.type.startsWith('pair') && (typeof event.patientId !== 'string' || !event.patientId || typeof event.nctId !== 'string' || !event.nctId || !checks(event.structuredChecks)))
      || (event.type === 'pairCompleted' && (typeof event.pairSummary !== 'string' || !summaries.has(event.pairSummary)))
      || (event.type === 'pairFailed' && (typeof event.failureCode !== 'string' || !event.failureCode || typeof event.failureExplanation !== 'string' || !event.failureExplanation))
      || (event.type === 'operationStopped' && (typeof event.stopReason !== 'string' || !stopReasons.has(event.stopReason))))
      throw new Error('Invalid progress message')
    operationId = event.operationId; sequence = event.sequence
    terminal = event.type === 'operationFinished' || event.type === 'operationStopped'
    receive(event)
  }
  try {
    while (true) {
      const { value, done } = await reader.read()
      buffer += done ? decoder.decode() : decoder.decode(value, { stream: true })
      let boundary: number
      while ((boundary = buffer.indexOf('\n')) >= 0) {
        const text = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 1)
        if (!text.trim()) throw new Error('Empty progress message')
        line(text)
      }
      if (buffer.length > 1024 * 1024) throw new Error('Oversized progress message')
      if (done) break
    }
    if (buffer.length || !terminal) throw new Error('Progress stream ended without confirmation')
  } catch (error) {
    await reader.cancel().catch(() => {})
    throw error
  } finally { reader.releaseLock() }
}
