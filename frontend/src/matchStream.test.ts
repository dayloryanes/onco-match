import { expect, test } from 'vitest'
import { readMatchStream } from './matchStream'
import type { MatchEvent } from './matchTypes'

const event = (type: string, sequence: number) => ({ type, sequence, batchId: 'b1', operationId: 'o1', version: 'v1', serverTime: '2026-11-03T12:00:00Z',
  status: 'unfinished', totalPairCount: 1, completedPairCount: 0, failedPairCount: 0, unfinishedPairCount: 1, activePairCount: 0, stopReason: 'cancelled', failureExplanation: 'Cancelled — café' })
function response(text: string) {
  const bytes = new TextEncoder().encode(text)
  return new Response(new ReadableStream({ start(controller) { for (const byte of bytes) controller.enqueue(Uint8Array.of(byte)); controller.close() } }),
    { headers: { 'Content-Type': 'application/x-ndjson; charset=utf-8' } })
}
test('Split UTF-8 characters and JSON lines are buffered until a complete message arrives', async () => {
  const received: MatchEvent[] = []
  await readMatchStream(response(JSON.stringify(event('operationStarted', 1)) + '\n' + JSON.stringify(event('operationStopped', 2)) + '\n'), 'b1', e => received.push(e))
  expect(received).toHaveLength(2)
  expect(received[1].failureExplanation).toBe('Cancelled — café')
})
test.each(['missing-terminal', 'sequence', 'counts', 'foreign-operation', 'status', 'summary'])('Broken stream %s requires reconciliation', async failure => {
  const first = event('operationStarted', 1)
  const last = { ...event('operationStopped', failure === 'sequence' ? 9 : 2),
    ...(failure === 'counts' ? { activePairCount: 2 } : {}), ...(failure === 'foreign-operation' ? { operationId: 'other' } : {}),
    ...(failure === 'status' ? { status: {} } : {}), ...(failure === 'summary' ? { type: 'pairCompleted', patientId: 'P', nctId: 'N', pairSummary: {} } : {}) }
  const text = JSON.stringify(first) + '\n' + (failure === 'missing-terminal' ? '' : JSON.stringify(last) + '\n')
  await expect(readMatchStream(response(text), 'b1', () => {})).rejects.toThrow()
})

test.each(['pairStarted-summary', 'pairCompleted-failure'])('Malformed optional payload %s is rejected before rendering', async failure => {
  const finding = { result: 'undetermined', explanation: 'Unknown.', patientEvidence: {}, studyEvidence: {} }
  const malformed = { ...event(failure.startsWith('pairStarted') ? 'pairStarted' : 'pairCompleted', 2), patientId: 'P', nctId: 'N',
    structuredChecks: { age: finding, sex: finding }, pairSummary: failure.startsWith('pairStarted') ? {} : 'conditionLabelAlignmentUndetermined',
    failureExplanation: failure.startsWith('pairCompleted') ? {} : null }
  const received: MatchEvent[] = []
  await expect(readMatchStream(response([event('operationStarted', 1), malformed, event('operationStopped', 3)].map(e => JSON.stringify(e)).join('\n') + '\n'), 'b1', e => received.push(e))).rejects.toThrow()
  expect(received).toHaveLength(1)
})
