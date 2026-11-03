import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from './api'
import type { CurrentUser } from './api'
import { sessionEventName } from './sessionEvents'
import type { SessionEvent, SessionTiming } from './sessionEvents'

function deadline(timing: SessionTiming, transit = 0) {
  const remaining = Date.parse(timing.expiresAt) - Date.parse(timing.serverTime) - transit
  return { timing, end: performance.now() + remaining }
}

function newerDeadline(current: ReturnType<typeof deadline>, timing: SessionTiming, transit = 0) {
  const expiresAt = Date.parse(timing.expiresAt)
  const currentExpiry = Date.parse(current.timing.expiresAt)
  if (expiresAt < currentExpiry) return current
  if (expiresAt === currentExpiry && Date.parse(timing.serverTime) < Date.parse(current.timing.serverTime)) return current
  return deadline(timing, transit)
}

export default function SessionControls({ user, onExpired }: { user: CurrentUser; onExpired: () => void }) {
  const [confirmed, setConfirmed] = useState(() => deadline(user))
  const [now, setNow] = useState(() => performance.now())
  const [pending, setPending] = useState(false)
  const [failed, setFailed] = useState(false)
  const extension = useRef<AbortController | null>(null)

  useEffect(() => {
    function receive(event: Event) {
      const update = (event as CustomEvent<{ event: SessionEvent }>).detail.event
      if (update.kind !== 'timing') return
      setConfirmed(current => newerDeadline(current, update.timing, Math.max(0, Date.now() - update.sentAt)))
      setNow(performance.now())
      if (update.renewed) setFailed(false)
    }
    window.addEventListener(sessionEventName, receive)
    const timer = window.setInterval(() => setNow(performance.now()), 1000)
    return () => {
      window.removeEventListener(sessionEventName, receive)
      window.clearInterval(timer)
      extension.current?.abort()
    }
  }, [])

  const remaining = confirmed.end - now

  async function extend() {
    if (extension.current) return
    const controller = new AbortController()
    extension.current = controller
    setPending(true)
    try {
      const renewed = await api.extendSession(controller.signal)
      if (controller.signal.aborted) return
      setConfirmed(current => newerDeadline(current, renewed))
      setNow(performance.now())
      setFailed(false)
    } catch (failure) {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else setFailed(true)
    } finally {
      if (!controller.signal.aborted) setPending(false)
      extension.current = null
    }
  }

  if (!Number.isFinite(confirmed.end)) return null
  return <section className="session-controls" aria-label="Session">
    <p>Session expires at <time dateTime={confirmed.timing.expiresAt}>{new Date(confirmed.timing.expiresAt).toLocaleTimeString()}</time>.</p>
    {(remaining <= 5 * 60_000 || failed) && <div className="session-warning" role="status">
      <p>Your session expires in {Math.max(0, Math.ceil(remaining / 60_000))} minutes. Stay signed in to continue.</p>
      {failed && <p role="alert">Unable to extend your session. The last confirmed expiry still applies.</p>}
      <button disabled={pending} onClick={extend}>{pending ? 'Extending session…' : failed ? 'Retry session extension' : 'Stay signed in'}</button>
    </div>}
  </section>
}
