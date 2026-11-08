import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent, ReactElement } from 'react'
import { BrowserRouter, Link, matchRoutes, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { api, ApiError } from './api'
import type { CurrentUser } from './api'
import './App.css'
import PatientPage from './PatientPage'
import StudyPage from './StudyPage'
import Dashboard from './Dashboard'
import { useListNavigation } from './listNavigation'
import SessionControls from './SessionControls'
import { sessionEvent, sessionEventName } from './sessionEvents'
import type { SessionEvent } from './sessionEvents'
import { useReadingPosition } from './useReadingPosition'

type PageRoute = {
  path: string
  title: string | ((params: Record<string, string | undefined>) => string)
  element: ReactElement
}

function returnDestination(value: unknown) {
  return typeof value === 'string' && /^(\/|\/(?:patients|studies)(?:\/[A-Za-z0-9_-]+)?)$/.test(value) ? value : '/'
}

function Login({ onLogin }: { onLogin: (user: CurrentUser) => void }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending) return
    if (!username.trim() || !password.trim()) {
      setError('Enter your username and password.')
      return
    }
    setError('')
    setPending(true)
    try {
      onLogin(await api.login(username, password))
    } catch (failure) {
      setError(failure instanceof ApiError && failure.code === 'invalid_credentials'
        ? 'Invalid username or password'
        : 'Unable to log in. Please try again.')
    } finally {
      setPending(false)
    }
  }

  return <section className="login-panel">
    <p className="eyebrow">Welcome to OncoMatch</p>
    <h1>Log in</h1>
    <p>Explore the oncology research screening demonstration.</p>
    <form onSubmit={submit}>
      <label htmlFor="username">Username</label>
      <input id="username" autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} required />
      <label htmlFor="password">Password</label>
      <input id="password" type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required />
      {error && <p role="alert">{error}</p>}
      <button disabled={pending}>{pending ? 'Logging in…' : 'Log in'}</button>
    </form>
    <aside className="demo-accounts" aria-label="Demo credentials">
      <h2>Try a demo account</h2>
      <p>
        <span className="fact-label">Viewer</span><br />
        <span className="fact-label">Username:</span> <code>viewer</code> · <span className="fact-label">Password:</span> <code>ViewerDemo1!</code>
      </p>
      <p>
        <span className="fact-label">Operator</span><br />
        <span className="fact-label">Username:</span> <code>operator</code> · <span className="fact-label">Password:</span> <code>OperatorDemo1!</code>
      </p>
    </aside>
  </section>
}

function SessionApp() {
  const patientsNavigation = useListNavigation()
  const studiesNavigation = useListNavigation()
  const clearPatientsNavigation = patientsNavigation.clear
  const clearStudiesNavigation = studiesNavigation.clear
  const clearNavigation = useCallback(() => {
    clearPatientsNavigation(); clearStudiesNavigation()
  }, [clearPatientsNavigation, clearStudiesNavigation])
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [checking, setChecking] = useState(true)
  const [sessionError, setSessionError] = useState(false)
  const [checkId, setCheckId] = useState(0)
  const [logoutPending, setLogoutPending] = useState(false)
  const [logoutError, setLogoutError] = useState(false)
  const [loginReturn, setLoginReturn] = useState<{ key: string; destination: string } | null>(null)
  const sessionEpoch = useRef(0)
  const location = useLocation()
  const preserveReadingPosition = useReadingPosition(location.key)
  const navigate = useNavigate()
  const expire = useCallback(() => { sessionEpoch.current++; clearNavigation(); setUser(null); setChecking(false); setSessionError(false) }, [clearNavigation])

  useEffect(() => {
    if (!window.BroadcastChannel) return
    const channel = new BroadcastChannel('oncomatch-session')
    channel.onmessage = (message: MessageEvent<SessionEvent>) => {
      if (message.data.kind === 'changed') {
        sessionEpoch.current++
        setChecking(true)
        setCheckId(value => value + 1)
      } else if (message.data.kind === 'timing') sessionEvent(message.data, false)
    }
    function publish(event: Event) {
      const detail = (event as CustomEvent<{ event: SessionEvent; broadcast: boolean }>).detail
      if (detail.broadcast && (detail.event.kind === 'changed' || detail.event.renewed)) channel.postMessage(detail.event)
    }
    window.addEventListener(sessionEventName, publish)
    return () => { channel.close(); window.removeEventListener(sessionEventName, publish) }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const epoch = sessionEpoch.current
    api.me(controller.signal).then(value => {
      if (!controller.signal.aborted && epoch === sessionEpoch.current) { setUser(value); setSessionError(false); setChecking(false) }
    }).catch(failure => {
      if (controller.signal.aborted || epoch !== sessionEpoch.current) return
      if (failure instanceof ApiError && failure.status === 401) { clearNavigation(); setUser(null); setSessionError(false) }
      else setSessionError(true)
      setChecking(false)
    })
    return () => controller.abort()
  }, [checkId, clearNavigation])

  useEffect(() => {
    function onFocus() {
      if (user && !logoutPending) {
        // Revalidate in the background: hiding controls here loses the click
        // that returned focus and collapses the page while the request runs.
        setCheckId(value => value + 1)
      }
    }
    window.addEventListener('focus', onFocus)
    return () => window.removeEventListener('focus', onFocus)
  }, [user, logoutPending])

  function finishLogin(value: CurrentUser) {
    sessionEpoch.current++
    clearNavigation()
    const requested = location.state?.returnTo ?? new URLSearchParams(location.search).get('returnTo')
    setLoginReturn({ key: location.key, destination: returnDestination(requested) })
    setUser(value)
  }

  function finishLogout() {
    sessionEpoch.current++
    clearNavigation()
    setUser(null)
    setChecking(false)
    setSessionError(false)
    setLogoutError(false)
    setLoginReturn(null)
    navigate('/login', { replace: true, state: null })
  }

  async function logout() {
    if (logoutPending) return
    sessionEpoch.current++
    setLogoutPending(true)
    setLogoutError(false)
    try {
      await api.logout()
      finishLogout()
    } catch {
      try { await api.me(); setLogoutError(true) }
      catch (failure) {
        if (failure instanceof ApiError && failure.status === 401) finishLogout()
        else setLogoutError(true)
      }
    } finally { setLogoutPending(false) }
  }

  // Every application route declares its title here, alongside its rendered page.
  const pageRoutes: PageRoute[] = [
    { path: '/', title: 'Dashboard', element: <Dashboard user={user!} onExpired={expire} preserveReadingPosition={preserveReadingPosition} /> },
    { path: '/login', title: 'Log in', element: <Navigate to={loginReturn?.key === location.key ? loginReturn.destination : '/'} replace /> },
    { path: '/patients', title: 'Patients', element: <PatientPage key={location.pathname} onExpired={expire} navigation={patientsNavigation} isVisible={!checking} /> },
    { path: '/patients/:id', title: params => `Patient ${params.id}`, element: <PatientPage key={location.pathname} onExpired={expire} navigation={patientsNavigation} isVisible={!checking} /> },
    { path: '/studies', title: 'Studies', element: <StudyPage key={location.pathname} onExpired={expire} navigation={studiesNavigation} isVisible={!checking} /> },
    { path: '/studies/:nctId', title: params => `Study ${params.nctId}`, element: <StudyPage key={location.pathname} onExpired={expire} navigation={studiesNavigation} isVisible={!checking} /> },
    { path: '*', title: 'Page not found', element: <h1>Page not found</h1> },
  ]
  const matchedPage = matchRoutes(pageRoutes, location)?.at(-1)
  const routeTitle = matchedPage && (typeof matchedPage.route.title === 'function'
    ? matchedPage.route.title(matchedPage.params) : matchedPage.route.title)
  const title = logoutPending ? 'Logging out'
    : checking ? 'Checking session'
      : sessionError ? 'Unable to check session'
        : !user ? 'Log in'
          : routeTitle ?? 'Page not found'
  useEffect(() => { document.title = `${title} — OncoMatch` }, [title])

  return <>
    <header className="site-header"><Link className="brand" to="/">OncoMatch<span>Research screening POC</span></Link>
      {user && !checking && !sessionError && !logoutPending && <nav className="main-navigation" aria-label="Main navigation"><Link to="/">Dashboard</Link><Link to="/patients">Patients</Link><Link to="/studies">Studies</Link></nav>}
      {user && !checking && !sessionError && <div className="account"><span>{user.username} · {user.role}</span><button className="secondary" disabled={logoutPending} onClick={logout}>{logoutPending ? 'Logging out…' : logoutError ? 'Retry logout' : 'Log out'}</button></div>}
    </header>
    <main>
      {user && !logoutPending && !sessionError && <SessionControls user={user} onExpired={expire} preserveReadingPosition={preserveReadingPosition} />}
      {logoutError && <p role="alert">Unable to confirm logout. Please retry.</p>}
      {logoutPending ? <p role="status">Logging out…</p>
        : checking ? <p role="status">Checking your session…</p>
        : sessionError ? <section><h1>Unable to check your session</h1><button onClick={() => { setChecking(true); setCheckId(value => value + 1) }}>Retry</button></section>
          : !user ? <Routes>
            <Route path="/login" element={<Login onLogin={finishLogin} />} />
            <Route path="*" element={<Navigate to="/login" state={{ returnTo: location.pathname }} replace />} />
          </Routes> : null}
      {user && !logoutPending && !sessionError && <div hidden={checking}>
            <Routes>
              {pageRoutes.map(page => <Route key={page.path} path={page.path} element={page.element} />)}
            </Routes>
      </div>}
    </main>
    <footer>Independent portfolio proof of concept. Fictional patients only. No clinical eligibility decisions.</footer>
  </>
}

export default function App() {
  return <BrowserRouter><SessionApp /></BrowserRouter>
}
