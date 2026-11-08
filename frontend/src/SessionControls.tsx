import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from './api'
import type { CurrentUser } from './api'
import { sessionEventName } from './sessionEvents'
import type { SessionEvent, SessionTiming } from './sessionEvents'
import type { PreserveReadingPosition } from './useReadingPosition'

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

export default function SessionControls({ user, onExpired, preserveReadingPosition }: { user: CurrentUser; onExpired: () => void; preserveReadingPosition: PreserveReadingPosition }) {
  const [confirmed, setConfirmed] = useState(() => deadline(user))
  const [now, setNow] = useState(() => performance.now())
  const [pending, setPending] = useState(false)
  const [failed, setFailed] = useState(false)
  const extension = useRef<AbortController | null>(null)

  useEffect(() => {
    function receive(event: Event) {
      const update = (event as CustomEvent<{ event: SessionEvent }>).detail.event
      if (update.kind !== 'timing') return
      preserveReadingPosition(() => {
        setConfirmed(current => newerDeadline(current, update.timing, Math.max(0, Date.now() - update.sentAt)))
        setNow(performance.now())
        if (update.renewed) setFailed(false)
      })
    }
    window.addEventListener(sessionEventName, receive)
    const timer = window.setInterval(() => setNow(performance.now()), 1000)
    return () => {
      window.removeEventListener(sessionEventName, receive)
      window.clearInterval(timer)
      extension.current?.abort()
    }
  }, [preserveReadingPosition])

  const remaining = confirmed.end - now
  const remainingSeconds = Math.max(0, Math.ceil(remaining / 1000))
  const remainingMinutes = Math.ceil(remainingSeconds / 60)
  const remainingText = remainingSeconds <= 60
    ? `${remainingSeconds} second${remainingSeconds === 1 ? '' : 's'}`
    : `${remainingMinutes} minute${remainingMinutes === 1 ? '' : 's'}`
  const shortSessionTesting = import.meta.env.DEV && import.meta.env.VITE_SESSION_TEST_MODE === 'true'
  const expiringSoon = remaining <= (shortSessionTesting ? 30_000 : 5 * 60_000)

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
    {(expiringSoon || failed) && <div className={expiringSoon ? 'session-warning' : 'session-extension-error'} role="status">
      {expiringSoon && <p>Your session expires in {remainingText}. Stay signed in to continue.</p>}
      {failed && <p role="alert">Unable to extend your session with this request. Your confirmed expiry is shown above.</p>}
      <button disabled={pending} onClick={extend}>{pending ? 'Extending session…' : failed ? 'Retry session extension' : 'Stay signed in'}</button>
    </div>}
  </section>
}
