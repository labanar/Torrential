import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  retries: 0,
  use: {
    baseURL: process.env.BASE_URL ?? 'http://app:8080',
    screenshot: 'off',
    trace: 'off',
  },
  projects: [
    {
      name: 'screenshots',
      use: {
        viewport: { width: 1280, height: 720 },
      },
    },
    {
      name: 'screenshots-mobile',
      use: {
        ...devices['iPhone 13'],
      },
    },
  ],
});
