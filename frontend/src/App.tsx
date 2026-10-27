import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { BrowserRouter, Link, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { api, ApiError } from './api'
import type { CurrentUser } from './api'
import './App.css'
import PatientPage from './PatientPage'

function returnDestination(value: unknown) {
  return typeof value === 'string' && /^(\/|\/patients(?:\/[A-Za-z0-9_-]+)?)$/.test(value) ? value : '/'
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
      <p>Viewer: <code>viewer</code> / <code>ViewerDemo!1</code></p>
      <p>Operator: <code>operator</code> / <code>OperatorDemo!1</code></p>
    </aside>
  </section>
}

function SessionApp() {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [checking, setChecking] = useState(true)
  const [sessionError, setSessionError] = useState(false)
  const [checkId, setCheckId] = useState(0)
  const [logoutPending, setLogoutPending] = useState(false)
  const [logoutError, setLogoutError] = useState(false)
  const [loginReturn, setLoginReturn] = useState<{ key: string; destination: string } | null>(null)
  const sessionEpoch = useRef(0)
  const location = useLocation()
  const navigate = useNavigate()
  const expire = useCallback(() => { sessionEpoch.current++; setUser(null) }, [])

  useEffect(() => {
    const controller = new AbortController()
    const epoch = sessionEpoch.current
    api.me(controller.signal).then(value => {
      if (!controller.signal.aborted && epoch === sessionEpoch.current) { setUser(value); setSessionError(false); setChecking(false) }
    }).catch(failure => {
      if (controller.signal.aborted || epoch !== sessionEpoch.current) return
      if (failure instanceof ApiError && failure.status === 401) { setUser(null); setSessionError(false) }
      else setSessionError(true)
      setChecking(false)
    })
    return () => controller.abort()
  }, [checkId])

  useEffect(() => {
    function onFocus() {
      if (user) { setChecking(true); setCheckId(value => value + 1) }
    }
    window.addEventListener('focus', onFocus)
    return () => window.removeEventListener('focus', onFocus)
  }, [user])

  function finishLogin(value: CurrentUser) {
    sessionEpoch.current++
    const requested = location.state?.returnTo ?? new URLSearchParams(location.search).get('returnTo')
    setLoginReturn({ key: location.key, destination: returnDestination(requested) })
    setUser(value)
  }

  function finishLogout() {
    sessionEpoch.current++
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

  return <>
    <header className="site-header"><Link className="brand" to="/">OncoMatch<span>Research screening POC</span></Link>
      {user && !checking && !sessionError && <div className="account"><span>{user.username} · {user.role}</span><button className="secondary" disabled={logoutPending} onClick={logout}>{logoutPending ? 'Logging out…' : logoutError ? 'Retry logout' : 'Log out'}</button></div>}
    </header>
    <main>
      {logoutError && <p role="alert">Unable to confirm logout. Please retry.</p>}
      {logoutPending ? <p role="status">Logging out…</p>
        : checking ? <p role="status">Checking your session…</p>
        : sessionError ? <section><h1>Unable to check your session</h1><button onClick={() => { setChecking(true); setCheckId(value => value + 1) }}>Retry</button></section>
          : !user ? <Routes>
            <Route path="/login" element={<Login onLogin={finishLogin} />} />
            <Route path="*" element={<Navigate to="/login" state={{ returnTo: location.pathname }} replace />} />
          </Routes>
            : <Routes>
              <Route path="/" element={<><p className="eyebrow">Your workspace</p><h1>Dashboard</h1><p>Browse the fictional patients available for this demonstration.</p><Link className="card" to="/patients">Browse patients</Link></>} />
              <Route path="/login" element={<Navigate to={loginReturn?.key === location.key ? loginReturn.destination : '/'} replace />} />
              <Route path="/patients" element={<PatientPage key={location.pathname} onExpired={expire} />} />
              <Route path="/patients/:id" element={<PatientPage key={location.pathname} onExpired={expire} />} />
              <Route path="*" element={<h1>Page not found</h1>} />
            </Routes>}
    </main>
    <footer>Independent portfolio proof of concept. Fictional patients only. No clinical eligibility decisions.</footer>
  </>
}

export default function App() {
  return <BrowserRouter><SessionApp /></BrowserRouter>
}
