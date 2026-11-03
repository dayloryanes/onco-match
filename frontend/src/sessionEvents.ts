export interface SessionTiming { expiresAt: string; serverTime: string }
export type SessionEvent =
  | { kind: 'timing'; timing: SessionTiming; renewed: boolean; sentAt: number }
  | { kind: 'changed' }

export const sessionEventName = 'oncomatch-session'

export function sessionEvent(event: SessionEvent, broadcast = true) {
  window.dispatchEvent(new CustomEvent(sessionEventName, { detail: { event, broadcast } }))
}
