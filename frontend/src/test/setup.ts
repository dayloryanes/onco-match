import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import { cleanup } from '@testing-library/react'
import { server } from './server'

const NativeBroadcastChannel = globalThis.BroadcastChannel
beforeEach(() => {
  // Node's channels cross worker/file boundaries; each jsdom test represents
  // its own browser. Keep channels within that test's browser identity.
  if (NativeBroadcastChannel) {
    const identity = crypto.randomUUID()
    vi.stubGlobal('BroadcastChannel', class extends NativeBroadcastChannel {
      constructor(name: string) { super(`${identity}:${name}`) }
    })
  }
})

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
  // jsdom has no layout/scrolling. Actual scroll behavior is checked in Chromium.
  vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
})
afterEach(() => {
  cleanup()
  server.resetHandlers()
  window.history.replaceState(null, '', '/')
})
afterAll(() => { server.close(); vi.restoreAllMocks(); vi.unstubAllGlobals() })
