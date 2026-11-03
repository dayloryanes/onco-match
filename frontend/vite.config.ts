import react from '@vitejs/plugin-react'
import { createLogger, defineConfig } from 'vite'
import { readFileSync } from 'node:fs'

export default defineConfig(({ command, mode }) => {
  const shortSessionTesting = command === 'serve' && mode === 'session-test'
  if (command === 'serve') {
    createLogger().info(shortSessionTesting
      ? 'Session warning: 30 seconds remaining (manual test mode). Use backend profile https-session-test and log out/in again.'
      : 'Session warning: 5 minutes remaining (standard mode).')
  }
  return {
    plugins: [react()],
    define: { 'import.meta.env.VITE_SESSION_TEST_MODE': JSON.stringify(String(shortSessionTesting)) },
    server: {
      host: 'localhost', port: 5173, strictPort: true,
      https: command === 'serve' ? {
        cert: readFileSync(new URL('../.certs/localhost.pem', import.meta.url)),
        key: readFileSync(new URL('../.certs/localhost.key', import.meta.url)),
      } : undefined,
      proxy: { '/api': { target: 'https://localhost:7132', secure: false } },
    },
  }
})
