import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll, vi } from 'vitest'
import { cleanup } from '@testing-library/react'
import { server } from './server'

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
afterAll(() => { server.close(); vi.restoreAllMocks() })
