import { sessionEvent } from './sessionEvents'
import type { SessionTiming } from './sessionEvents'
import type { MatchBatch, MatchEvent, MatchPair, MatchState, PairDetails } from './matchTypes'
import { readMatchStream } from './matchStream'

export interface CurrentUser extends SessionTiming { username: string; role: 'Viewer' | 'Operator' }
export interface StudySummary {
  nctId: string; title: string | null; recruitmentStatus: string | null; studyType: string | null; conditions: string[] | null
}
export interface StudyDetails extends StudySummary {
  briefSummary: string | null; minimumAge: string | null; maximumAge: string | null; sex: string | null; registryUrl: string
}
export interface ImportAttempt {
  id: string; startedAt: string; finishedAt: string | null; status: 'running' | 'completed' | 'failed' | 'cancelled'
  retryOfAttemptId: string | null; fetchedCount: number | null; importedCount: number | null
  duplicateCount: number | null; rejectedCount: number | null; importedNctIds: string[]; duplicateNctIds: string[]
  rejections: { recordNumber: number; nctId: string | null; code: string; reason: string }[]; failureCode: string | null
}
export interface ImportState {
  version: string; isImporting: boolean; isExhausted: boolean; nextAction: 'initial' | 'next' | 'retry' | 'none'
  totalImportedStudyCount: number; attempts: ImportAttempt[]
}
export interface PatientSummary { id: string; ageYears: number | null; sex: string; cancerType: string | null }
export interface PatientDetails extends PatientSummary {
  stage: { status: 'known' | 'unknown' | 'notApplicable'; value: string | null; classificationSystem: string | null }
  diseaseState: { status: 'known' | 'unknown' | 'notApplicable'; value: string | null }
}

export class ApiError extends Error {
  status: number
  code: string
  constructor(status: number, code: string, message: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(new URL(`/api${path}`, window.location.origin), {
    credentials: 'same-origin', ...options,
  })
  if (!options.signal?.aborted) {
    const expiresAt = response.headers.get('X-Session-Expires-At')
    const serverTime = response.headers.get('X-Server-Time')
    if (expiresAt && serverTime) sessionEvent({ kind: 'timing', timing: { expiresAt, serverTime },
      renewed: response.headers.get('X-Session-Renewed') === 'true', sentAt: Date.now() })
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw new ApiError(response.status, problem.code ?? 'request_failed', problem.title ?? 'Request failed')
  }
  const result = response.status === 204 || response.status === 202 ? undefined : await response.json()
  // Session endpoints also expose timing in their public JSON contract.
  if (!options.signal?.aborted && !response.headers.has('X-Session-Expires-At')
    && typeof result?.expiresAt === 'string' && typeof result?.serverTime === 'string') {
    sessionEvent({ kind: 'timing', timing: result, renewed: path === '/auth/extend-session' || path === '/auth/login', sentAt: Date.now() })
  }
  return result as T
}

async function csrf(signal?: AbortSignal) {
  return request<{ requestToken: string }>('/auth/csrf', { signal })
}

async function post<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const token = await csrf(signal)
  return request<T>(path, {
    method: 'POST', signal,
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token.requestToken },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

export const api = {
  me: (signal?: AbortSignal) => request<CurrentUser>('/auth/me', { signal }),
  logout: async (signal?: AbortSignal) => {
    await post<void>('/auth/logout', undefined, signal)
    sessionEvent({ kind: 'changed' })
  },
  extendSession: (signal?: AbortSignal) => post<CurrentUser>('/auth/extend-session', undefined, signal),
  login: async (username: string, password: string, signal?: AbortSignal) => {
    const user = await post<CurrentUser>('/auth/login', { username, password }, signal)
    sessionEvent({ kind: 'changed' })
    try { await csrf(signal) }
    catch (failure) {
      if (signal?.aborted) throw failure
      // Login already set the cookie. Confirm that session instead of claiming
      // credentials failed; the next explicit mutation acquires a fresh token.
      return request<CurrentUser>('/auth/me', { signal })
    }
    return user
  },
  patients: (signal?: AbortSignal) => request<PatientSummary[]>('/patients', { signal }),
  patient: (id: string, signal?: AbortSignal) => request<PatientDetails>(`/patients/${encodeURIComponent(id)}`, { signal }),
  studies: (signal?: AbortSignal) => request<StudySummary[]>('/studies', { signal }),
  study: (id: string, signal?: AbortSignal) => request<StudyDetails>(`/studies/${encodeURIComponent(id)}`, { signal }),
  importState: (signal?: AbortSignal) => request<ImportState>('/study-imports', { signal }),
  importStudies: (expectedVersion: string, signal?: AbortSignal) => post<ImportState>('/study-imports', { expectedVersion }, signal),
}

export const matchApi = {
  pairs: (filter: 'patientId' | 'nctId', id: string, signal?: AbortSignal) => request<MatchPair[]>(`/match/pairs?${filter}=${encodeURIComponent(id)}`, { signal }),
  batches: (signal?: AbortSignal) => request<MatchState>('/match/batches', { signal }),
  batch: (id: string, signal?: AbortSignal) => request<MatchBatch>(`/match/batches/${encodeURIComponent(id)}`, { signal }),
  pair: (patientId: string, nctId: string, signal?: AbortSignal) => request<PairDetails>(`/match/pairs/${encodeURIComponent(patientId)}/${encodeURIComponent(nctId)}`, { signal }),
  cancel: (operationId: string, signal?: AbortSignal) => post<void>(`/match/operations/${encodeURIComponent(operationId)}/cancel`, undefined, signal),
  operate: async (batch: MatchBatch, action: string, signal: AbortSignal, receive: (event: MatchEvent) => void) => {
    const token = await csrf(signal)
    const response = await fetch(new URL(`/api/match/batches/${encodeURIComponent(batch.batchId)}/operations`, window.location.origin), {
      method: 'POST', credentials: 'same-origin', signal,
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token.requestToken },
      body: JSON.stringify({ action, expectedVersion: batch.version }),
    })
    const expiresAt = response.headers.get('X-Session-Expires-At'), serverTime = response.headers.get('X-Server-Time')
    if (!signal.aborted && expiresAt && serverTime) sessionEvent({ kind: 'timing', timing: { expiresAt, serverTime }, renewed: response.headers.get('X-Session-Renewed') === 'true', sentAt: Date.now() })
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}))
      throw new ApiError(response.status, problem.code ?? 'request_failed', problem.title ?? 'Unable to start match')
    }
    await readMatchStream(response, batch.batchId, receive)
  },
}
