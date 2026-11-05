export function studySex(value: string | null) {
  const labels: Record<string, string> = { ALL: 'All', MALE: 'Male', FEMALE: 'Female' }
  return value === null ? null : labels[value] ?? value
}
