import { defineConfig, devices } from '@playwright/test'
import { fileURLToPath } from 'node:url'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  use: { baseURL: 'https://localhost:5173', ignoreHTTPSErrors: true, trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'dotnet run --no-launch-profile --project ../backend/src/OncoMatch.Api --urls https://localhost:7132',
      url: 'https://localhost:7132/api/auth/csrf', ignoreHTTPSErrors: true,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_Kestrel__Certificates__Default__Path: fileURLToPath(new URL('../.certs/localhost.pem', import.meta.url)),
        ASPNETCORE_Kestrel__Certificates__Default__KeyPath: fileURLToPath(new URL('../.certs/localhost.key', import.meta.url)),
      },
    },
    { command: 'npm run dev', url: 'https://localhost:5173', ignoreHTTPSErrors: true, reuseExistingServer: false },
  ],
})
