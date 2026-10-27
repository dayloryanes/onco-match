import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import App from './App'
import { server } from './test/server'

const patient = {
  id: 'P001', ageYears: 7, sex: 'female', cancerType: 'Acute lymphoblastic leukemia',
  stage: { status: 'notApplicable', value: null, classificationSystem: null },
  diseaseState: { status: 'known', value: 'newlyDiagnosed' },
}

function signedIn() {
  server.use(http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })))
}

test('Patients opens directly addressable details with distinct missing-fact labels', async () => {
  signedIn()
  server.use(
    http.get('*/api/patients', () => HttpResponse.json([patient])),
    http.get('*/api/patients/P001', () => HttpResponse.json(patient)),
  )
  window.history.replaceState(null, '', '/patients')
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('link', { name: 'P001' }))
  expect(await screen.findByRole('heading', { name: 'Patient P001' })).toBeVisible()
  expect(window.location.pathname).toBe('/patients/P001')
  expect(screen.getAllByText('Not applicable')).toHaveLength(2)
  expect(screen.getByText('Newly diagnosed')).toBeVisible()
})

test('ordinary login opens Dashboard with the current role and Patients navigation', async () => {
  server.use(http.post('*/api/auth/login', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })))
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), 'viewer')
  await user.type(screen.getByLabelText('Password'), 'ViewerDemo!1')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
  expect(screen.getByText('viewer · Viewer')).toBeVisible()
  expect(screen.getByRole('link', { name: 'Browse patients' })).toHaveAttribute('href', '/patients')
})

test('successful logout returns to Login and a protected link requires login again', async () => {
  signedIn()
  server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Log out' }))
  expect(await screen.findByLabelText('Username')).toBeVisible()
  expect(screen.queryByText('viewer · Viewer')).not.toBeInTheDocument()
  expect(window.location.pathname).toBe('/login')
})

test.each([
  [401, 'invalid_credentials', 'Invalid username or password'],
  [503, 'unavailable', 'Unable to log in. Please try again.'],
])('login distinguishes error status %s', async (status, code, message) => {
  server.use(http.post('*/api/auth/login', () => HttpResponse.json({ code }, { status })))
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), 'viewer')
  await user.type(screen.getByLabelText('Password'), 'wrong')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.getByRole('button', { name: 'Log in' })).toBeEnabled()
})

test('whitespace-only credentials receive required-field feedback', async () => {
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), '   ')
  await user.type(screen.getByLabelText('Password'), 'something')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Enter your username and password.')
})

test('a protected patient deep link returns to that patient after login', async () => {
  window.history.replaceState(null, '', '/patients/P001')
  server.use(
    http.post('*/api/auth/login', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/patients/P001', () => HttpResponse.json(patient)),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), 'viewer')
  await user.type(screen.getByLabelText('Password'), 'ViewerDemo!1')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('heading', { name: 'Patient P001' })).toBeVisible()
})

test('an external return URL is ignored', async () => {
  window.history.replaceState(null, '', '/login?returnTo=https://example.org')
  server.use(http.post('*/api/auth/login', () => HttpResponse.json({ username: 'operator', role: 'Operator' })))
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), 'operator')
  await user.type(screen.getByLabelText('Password'), 'OperatorDemo!1')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
  expect(window.location.pathname).toBe('/')
  expect(screen.getByText('operator · Operator')).toBeVisible()
})

test('a failed list request offers Retry instead of rendering an empty list', async () => {
  signedIn()
  window.history.replaceState(null, '', '/patients')
  server.use(http.get('*/api/patients', () => HttpResponse.json({}, { status: 503 })))
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load patient information.')
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
  server.use(http.get('*/api/patients', () => HttpResponse.json([patient])))
  await user.click(screen.getByRole('button', { name: 'Retry' }))
  expect(await screen.findByRole('link', { name: 'P001' })).toBeVisible()
})

test('an unknown patient shows Patient not found', async () => {
  signedIn()
  window.history.replaceState(null, '', '/patients/missing')
  server.use(http.get('*/api/patients/missing', () => HttpResponse.json({}, { status: 404 })))
  render(<App />)
  expect(await screen.findByRole('heading', { name: 'Patient not found' })).toBeVisible()
})

test('an expired API session clears protected content and opens login', async () => {
  signedIn()
  window.history.replaceState(null, '', '/patients')
  server.use(http.get('*/api/patients', () => HttpResponse.json({}, { status: 401 })))
  render(<App />)
  expect(await screen.findByLabelText('Username')).toBeVisible()
  expect(screen.queryByText('viewer · Viewer')).not.toBeInTheDocument()
})

test('failed logout stays signed in and offers an explicit retry', async () => {
  signedIn()
  server.use(http.post('*/api/auth/logout', () => HttpResponse.error()))
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Log out' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to confirm logout.')
  expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
  server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
  await user.click(screen.getByRole('button', { name: 'Retry logout' }))
  expect(await screen.findByLabelText('Username')).toBeVisible()
})

test('focusing a tab hides protected content until its session is checked', async () => {
  signedIn()
  render(<App />)
  await screen.findByRole('heading', { name: 'Dashboard' })
  let release!: () => void
  const wait = new Promise<void>(resolve => { release = resolve })
  server.use(http.get('*/api/auth/me', async () => { await wait; return HttpResponse.json({}, { status: 401 }) }))
  fireEvent.focus(window)
  expect(screen.getByRole('status')).toHaveTextContent('Checking your session')
  expect(screen.queryByRole('heading', { name: 'Dashboard' })).not.toBeInTheDocument()
  await act(async () => release())
  expect(await screen.findByLabelText('Username')).toBeVisible()
})

test('late patient responses cannot restore content after logout', async () => {
  signedIn()
  window.history.replaceState(null, '', '/patients')
  let release!: () => void
  const wait = new Promise<void>(resolve => { release = resolve })
  server.use(
    http.get('*/api/patients', async () => { await wait; return HttpResponse.json([patient]) }),
    http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
  )
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByText('Loading patients…')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Log out' }))
  expect(await screen.findByLabelText('Username')).toBeVisible()
  await act(async () => release())
  expect(screen.queryByRole('link', { name: 'P001' })).not.toBeInTheDocument()
})

test('login cannot be submitted again while its request is pending', async () => {
  let release!: () => void
  const wait = new Promise<void>(resolve => { release = resolve })
  server.use(http.post('*/api/auth/login', async () => {
    await wait
    return HttpResponse.json({ username: 'viewer', role: 'Viewer' })
  }))
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), 'viewer')
  await user.type(screen.getByLabelText('Password'), 'ViewerDemo!1')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('button', { name: 'Logging in…' })).toBeDisabled()
  await act(async () => release())
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
})

test('session discovery failure offers Retry without exposing protected content', async () => {
  server.use(http.get('*/api/auth/me', () => HttpResponse.json({}, { status: 503 })))
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByRole('heading', { name: 'Unable to check your session' })).toBeVisible()
  expect(screen.queryByRole('link', { name: 'Browse patients' })).not.toBeInTheDocument()
  signedIn()
  await user.click(screen.getByRole('button', { name: 'Retry' }))
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
})

test('unknown facts and a supplied stage without a system retain their distinct labels', async () => {
  signedIn()
  window.history.replaceState(null, '', '/patients/P001')
  server.use(http.get('*/api/patients/P001', () => HttpResponse.json({
    ...patient, ageYears: null, sex: 'unknown',
    stage: { status: 'known', value: 'II', classificationSystem: null },
    diseaseState: { status: 'unknown', value: null },
  })))
  render(<App />)
  await screen.findByRole('heading', { name: 'Patient P001' })
  expect(screen.getAllByText('Unknown')).toHaveLength(3)
  expect(screen.getByText('II')).toBeVisible()
  expect(screen.getByText('System unknown')).toBeVisible()
})

test('patient content is withheld while logout is pending, including on focus', async () => {
  signedIn()
  window.history.replaceState(null, '', '/patients/P001')
  let release!: () => void
  const wait = new Promise<void>(resolve => { release = resolve })
  server.use(
    http.get('*/api/patients/P001', () => HttpResponse.json(patient)),
    http.post('*/api/auth/logout', async () => { await wait; return new HttpResponse(null, { status: 204 }) }),
  )
  const user = userEvent.setup()
  render(<App />)
  await screen.findByRole('heading', { name: 'Patient P001' })
  await user.click(screen.getByRole('button', { name: 'Log out' }))
  fireEvent.focus(window)
  expect(screen.queryByText('Acute lymphoblastic leukemia')).not.toBeInTheDocument()
  await act(async () => release())
  expect(await screen.findByLabelText('Username')).toBeVisible()
})

test('successful login survives a failed CSRF refresh by confirming its session', async () => {
  server.use(http.post('*/api/auth/login', () => {
    server.use(
      http.get('*/api/auth/csrf', () => HttpResponse.json({}, { status: 503 })),
      http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    )
    return HttpResponse.json({ username: 'viewer', role: 'Viewer' })
  }))
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), 'viewer')
  await user.type(screen.getByLabelText('Password'), 'ViewerDemo!1')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
})
