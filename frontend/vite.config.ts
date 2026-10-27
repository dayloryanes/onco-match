import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { readFileSync } from 'node:fs'

// https://vite.dev/config/
export default defineConfig(({ command }) => ({
  plugins: [react()],
  server: {
    host: 'localhost', port: 5173, strictPort: true,
    https: command === 'serve' ? {
      cert: readFileSync(new URL('../.certs/localhost.pem', import.meta.url)),
      key: readFileSync(new URL('../.certs/localhost.key', import.meta.url)),
    } : undefined,
    proxy: { '/api': { target: 'https://localhost:7132', secure: false } },
  },
}))
