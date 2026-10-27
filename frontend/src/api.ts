export interface CurrentUser { username: string; role: 'Viewer' | 'Operator' }
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
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw new ApiError(response.status, problem.code ?? 'request_failed', problem.title ?? 'Request failed')
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
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
  logout: (signal?: AbortSignal) => post<void>('/auth/logout', undefined, signal),
  login: async (username: string, password: string, signal?: AbortSignal) => {
    const user = await post<CurrentUser>('/auth/login', { username, password }, signal)
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
}
