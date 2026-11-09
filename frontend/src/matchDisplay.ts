const names: Record<string, string> = {
  noWork: 'No work', notStarted: 'Not started', running: 'Running', stopping: 'Stopping', unfinished: 'Unfinished',
  finishedWithFailures: 'Finished with failures', completed: 'Completed', failed: 'Failed', interrupted: 'Interrupted',
  excludedByAgeSex: 'Excluded by age/sex', conditionLabelAlignmentSupported: 'Condition-label alignment supported',
  mixedConditionLabelFindings: 'Mixed condition-label findings', conditionLabelConflictsIdentified: 'Condition-label conflicts identified',
  conditionLabelAlignmentUndetermined: 'Condition-label alignment undetermined', alignmentSupported: 'Alignment supported',
  conflictIdentified: 'Conflict identified', undetermined: 'Undetermined', notSpecified: 'Not specified in condition labels',
  satisfied: 'Satisfied', excluded: 'Excluded', noRestrictionIdentified: 'No restriction identified',
  sessionExpired: 'Session expired', disconnected: 'Connection lost', cancelled: 'Cancelled', logout: 'Logged out',
  inferenceUnavailable: 'The selected local inference service or model is unavailable. Check setup before continuing.',
  internalError: 'The operation stopped after an unexpected error.',
}
export const label = (value: string | null) => value ? names[value] ?? value : 'Not assessed'
export const limits = "This assessment checks structured age and sex requirements and compares cancer type, stage, and disease state with the study's listed conditions. Biomarkers, treatment history, other requirements, and free-text eligibility criteria are not assessed. Results do not establish clinical eligibility or enrollment approval."
