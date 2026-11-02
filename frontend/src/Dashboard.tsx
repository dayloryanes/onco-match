import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from './api'
import type { CurrentUser, ImportAttempt, ImportState } from './api'

const errors: Record<string, string> = {
  import_in_progress: 'Another import is running. Refresh to check its progress.',
  import_state_changed: 'Import state changed in another session. Review the refreshed history before importing.',
  imports_exhausted: 'No more registry pages.',
  registry_timeout: 'The registry request timed out. You can explicitly retry.',
  registry_unavailable: 'The registry is unavailable. You can explicitly retry.',
  registry_invalid_response: 'The registry returned an invalid response. You can explicitly retry.',
  csrf_invalid: 'Refresh the page and try again.',
  access_denied: 'Your account cannot import studies.',
}

function Attempt({ attempt }: { attempt: ImportAttempt }) {
  return <li className="import-attempt">
    <h3>{attempt.status[0].toUpperCase() + attempt.status.slice(1)} import</h3>
    <p><time dateTime={attempt.startedAt}>{new Date(attempt.startedAt).toLocaleString()}</time>
      {attempt.finishedAt && <> · Finished <time dateTime={attempt.finishedAt}>{new Date(attempt.finishedAt).toLocaleString()}</time></>}</p>
    <p className="attempt-id">Attempt {attempt.id}{attempt.retryOfAttemptId && <> · Retry of {attempt.retryOfAttemptId}</>}</p>
    <p>Fetched: {attempt.fetchedCount ?? 'Not available'} · Imported: {attempt.importedCount ?? 'Not available'} · Duplicates: {attempt.duplicateCount ?? 'Not available'} · Rejected: {attempt.rejectedCount ?? 'Not available'}</p>
    {attempt.failureCode && <p>{errors[attempt.failureCode] ?? 'Import failed. You can explicitly retry.'}</p>}
    {attempt.status === 'cancelled' && <p>Import cancelled. No staged studies were saved.</p>}
    {attempt.importedNctIds.length > 0 && <p>Imported: {attempt.importedNctIds.join(', ')}</p>}
    {attempt.duplicateNctIds.length > 0 && <p>Duplicates: {attempt.duplicateNctIds.join(', ')}</p>}
    {attempt.rejections.length > 0 && <ul>{attempt.rejections.map(rejection => <li key={rejection.recordNumber}>
      Record {rejection.recordNumber}: {rejection.reason}{rejection.nctId && <> ({rejection.nctId})</>}
    </li>)}</ul>}
  </li>
}

export default function Dashboard({ user, onExpired }: { user: CurrentUser; onExpired: () => void }) {
  const [state, setState] = useState<ImportState | null>(null)
  const [loading, setLoading] = useState(true)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [refresh, setRefresh] = useState(0)
  const operation = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    operation.current = controller
    api.importState(controller.signal).then(value => {
      if (!controller.signal.aborted) { setState(value); setLoading(false) }
    }).catch(failure => {
      if (controller.signal.aborted) return
      if (failure instanceof ApiError && failure.status === 401) onExpired()
      else { setError('Unable to load import history.'); setLoading(false) }
    })
    return () => { controller.abort(); operation.current?.abort() }
  }, [refresh, onExpired])

  function reload() {
    setError(''); setLoading(true); setState(null); setRefresh(value => value + 1)
  }

  async function importPage() {
    if (!state || pending || operation.current?.signal.aborted || state.nextAction === 'none') return
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
      setError(failure instanceof ApiError ? errors[failure.code] ?? 'Import failed. Review the current history before retrying.' : 'Unable to confirm the import. Review the current history before retrying.')
      try {
        const value = await api.importState(controller.signal)
        if (!controller.signal.aborted) setState(value)
      } catch (reloadFailure) {
        if (controller.signal.aborted) return
        if (reloadFailure instanceof ApiError && reloadFailure.status === 401) onExpired()
        else setError('Unable to reload import history. Refresh before importing again.')
      }
    } finally { if (!controller.signal.aborted) setPending(false) }
  }

  return <section>
    <p className="eyebrow">Your workspace</p><h1>Dashboard</h1>
    <p>Import registry studies and browse the shared demonstration data.</p>
    <p className="notice">Fixed cancer search: Recruiting and Not Yet Recruiting interventional and observational studies, in registry relevance order. Relevance is to the search, not to a patient. No age or geography filter is applied, and no pediatric/adult distribution is guaranteed. Importing does not start matching.</p>
    <div className="actions"><Link to="/patients">Browse patients</Link><Link to="/studies">Browse studies</Link>
      <button onClick={reload} disabled={loading || pending}>{state ? 'Refresh' : 'Retry'}</button></div>
    <h2>Study imports</h2>
    {error && <p role="alert">{error}</p>}
    {loading && <p role="status">Loading import history…</p>}
    {state && <>
      <p>{state.totalImportedStudyCount} imported studies · Shared until backend restart</p>
      {(pending || state.isImporting) && <p role="status">Importing…</p>}
      {state.isExhausted && <p>No more registry pages.</p>}
      {user.role === 'Operator' && <button onClick={importPage} disabled={pending || state.isImporting || state.isExhausted || state.nextAction === 'none'}>
        {pending || state.isImporting ? 'Importing…' : state.nextAction === 'initial' ? 'Import first 10' : state.nextAction === 'retry' ? 'Retry import' : 'Load Next 10'}
      </button>}
      <h2 className="history-heading">Import history</h2>
      {state.attempts.length === 0 ? <p>No import attempts yet.</p> : <ol className="import-history">{state.attempts.map(attempt => <Attempt key={attempt.id} attempt={attempt} />)}</ol>}
    </>}
  </section>
}
