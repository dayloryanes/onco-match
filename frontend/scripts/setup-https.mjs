import { execFileSync } from 'node:child_process'
import { mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const directory = new URL('../../.certs/', import.meta.url)
mkdirSync(directory, { recursive: true })
execFileSync('dotnet', ['dev-certs', 'https', '--export-path',
  fileURLToPath(new URL('localhost.pem', directory)), '--format', 'Pem', '--no-password'], { stdio: 'inherit' })
console.log('Local HTTPS certificate exported. See README for the optional trust step.')
