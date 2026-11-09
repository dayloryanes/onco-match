import type { ImportAttempt } from './api'

import { importErrors } from './importErrors'

export default function ImportAttemptDetails({ attempt }: { attempt: ImportAttempt }) {
  const headingId = `import-attempt-${attempt.id}`
  const identity = <p className="attempt-id"><span className="fact-label">Attempt:</span> {attempt.id}{attempt.retryOfAttemptId && <> · <span className="fact-label">Retry of:</span> {attempt.retryOfAttemptId}</>}</p>
  return <div className="import-details">
    <h2 className="history-card-heading" id={headingId}>{attempt.status[0].toUpperCase() + attempt.status.slice(1)} import</h2>
    {attempt.status !== 'completed' && identity}
    <p><span className="fact-label">Started:</span> <time dateTime={attempt.startedAt}>{new Date(attempt.startedAt).toLocaleString()}</time>
      {attempt.finishedAt && <> · <span className="fact-label">Finished:</span> <time dateTime={attempt.finishedAt}>{new Date(attempt.finishedAt).toLocaleString()}</time></>}</p>
    <p><span className="fact-label">Fetched:</span> {attempt.fetchedCount ?? 'Not available'} · <span className="fact-label">Imported:</span> {attempt.importedCount ?? 'Not available'} · <span className="fact-label">Duplicates:</span> {attempt.duplicateCount ?? 'Not available'} · <span className="fact-label">Rejected:</span> {attempt.rejectedCount ?? 'Not available'}</p>
    {attempt.failureCode && <p>{importErrors[attempt.failureCode] ?? 'Import failed. You can explicitly retry.'}</p>}
    {attempt.status === 'cancelled' && <p>Import cancelled. No staged studies were saved.</p>}
    {attempt.importedNctIds.length > 0 && <p><span className="fact-label">Imported studies:</span> {attempt.importedNctIds.join(', ')}</p>}
    {attempt.duplicateNctIds.length > 0 && <p><span className="fact-label">Duplicates:</span> {attempt.duplicateNctIds.join(', ')}</p>}
    {attempt.rejections.length > 0 && <ul>{attempt.rejections.map(rejection => <li key={rejection.recordNumber}>
      <span className="fact-label">Record {rejection.recordNumber}:</span> {rejection.reason}{rejection.nctId && <> ({rejection.nctId})</>}
    </li>)}</ul>}
    {attempt.status === 'completed' && identity}
  </div>
}
