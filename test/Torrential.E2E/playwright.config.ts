import { defineConfig, devices } from '@playwright/test';

const isLocal = !process.env.BASE_URL || process.env.BASE_URL.includes('localhost');

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  retries: 0,
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5173',
    screenshot: 'off',
    trace: 'off',
  },
  projects: [
    {
      name: 'screenshots',
      testMatch: 'screenshots.spec.ts',
      use: {
        baseURL: process.env.BASE_URL ?? 'http://app:8080',
        viewport: { width: 1280, height: 720 },
      },
    },
    {
      name: 'screenshots-mobile',
      testMatch: 'screenshots.spec.ts',
      use: {
        baseURL: process.env.BASE_URL ?? 'http://app:8080',
        ...devices['iPhone 13'],
      },
    },
    {
      name: 'ui-desktop',
      testMatch: ['ui-screenshots.spec.ts', 'detail-pane-screenshot.spec.ts'],
      use: {
        viewport: { width: 1280, height: 720 },
      },
    },
    {
      name: 'ui-mobile',
      testMatch: 'ui-screenshots.spec.ts',
      use: {
        ...devices['Pixel 5'],
      },
    },
  ],
  ...(isLocal
    ? {
        webServer: {
          command: 'npm run dev',
          cwd: '../../src/torrential-ui-vite',
          port: 5173,
          reuseExistingServer: true,
        },
      }
    : {}),
});
