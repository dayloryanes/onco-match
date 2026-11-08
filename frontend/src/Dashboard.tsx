import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from './api'
import type { CurrentUser, ImportState } from './api'
import MatchDashboard from './MatchDashboard'
import { importErrors as errors } from './importErrors'
import type { PreserveReadingPosition } from './useReadingPosition'

export default function Dashboard({ user, onExpired, preserveReadingPosition }: { user: CurrentUser; onExpired: () => void; preserveReadingPosition: PreserveReadingPosition }) {
  const [state, setState] = useState<ImportState | null>(null)
  const [loading, setLoading] = useState(true)
  const [historyConfirmed, setHistoryConfirmed] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [refresh, setRefresh] = useState(0)
  const operation = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    operation.current = controller
    api.importState(controller.signal).then(value => {
      if (!controller.signal.aborted) {
        const apply = () => { setState(value); setHistoryConfirmed(true); setLoading(false) }
        if (refresh > 0) preserveReadingPosition(apply)
        else apply()
      }
    }).catch(failure => {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else {
        const apply = () => { setError('Unable to load import history.'); setHistoryConfirmed(false); setLoading(false) }
        if (refresh > 0) preserveReadingPosition(apply)
        else apply()
      }
    })
    return () => { controller.abort(); operation.current?.abort() }
  }, [refresh, onExpired, preserveReadingPosition])

  function reload() {
    preserveReadingPosition(() => { setError(''); setHistoryConfirmed(false); setLoading(true); setRefresh(value => value + 1) })
  }

  async function importPage() {
    if (!state || !historyConfirmed || loading || pending || operation.current?.signal.aborted || state.nextAction === 'none') return
    const controller = new AbortController()
    operation.current = controller
    setPending(true); setError('')
    try {
      const value = await api.importStudies(state.version, controller.signal)
      if (!controller.signal.aborted) setState(value)
    } catch (failure) {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) { onExpired(); return }
      // A lost response may conceal a committed import. Reconcile before offering
      // another explicit POST; never resubmit automatically.
      setState(null)
      setHistoryConfirmed(false)
      setError(failure instanceof ApiError ? errors[failure.code] ?? 'Import failed. Review the current history before retrying.' : 'Unable to confirm the import. Review the current history before retrying.')
      try {
        const value = await api.importState(controller.signal)
        if (!controller.signal.aborted) { setState(value); setHistoryConfirmed(true) }
      } catch (reloadFailure) {
        if (controller.signal.aborted) return
        if (reloadFailure instanceof ApiError && reloadFailure.status === 401) onExpired()
        else setError('Unable to reload import history. Refresh before importing again.')
      }
    } finally { if (!controller.signal.aborted) setPending(false) }
  }

  return <section>
    <p className="eyebrow">Your workspace</p><h1>Dashboard</h1>
    <p>Import studies · Run assessments · Review results</p>
    <p className="notice">Fixed cancer search: Recruiting and Not Yet Recruiting interventional and observational studies, in registry relevance order. Relevance is to the search, not to a patient. No age, sex, or geography filter is applied, and no pediatric/adult distribution is guaranteed. Importing does not start a match operation.</p>
    <section aria-label="Study imports">
      {error && <p role="alert">{error}</p>}
      {loading && !state && <p role="status">Loading import history…</p>}
      {state && (pending || state.isImporting) && <p role="status">Importing…</p>}
      {state?.isExhausted && <p>No more registry pages.</p>}
      <div className="actions import-actions">
        {state && user.role === 'Operator' && <button onClick={importPage} disabled={!historyConfirmed || loading || pending || state.isImporting || state.isExhausted || state.nextAction === 'none'}>
          {pending || state.isImporting ? 'Importing…' : state.nextAction === 'initial' ? 'Import first 10' : state.nextAction === 'retry' ? 'Retry import' : 'Load Next 10'}
        </button>}
        <button className="quiet-button" onClick={reload} disabled={loading || pending}>{state ? 'Refresh' : 'Retry'}</button>
      </div>
      <p>{state && <><strong>{state.totalImportedStudyCount} {state.totalImportedStudyCount === 1 ? 'study' : 'studies'}</strong> · </>}Import history and assessment results are shared across sessions and cleared when the server restarts.</p>
    </section>
    <MatchDashboard user={user} onExpired={onExpired} imports={state?.attempts} importVersion={state?.version} importReady={state !== null} refreshId={refresh} preserveReadingPosition={preserveReadingPosition} />

  </section>
}
