import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  retries: 0,
  use: {
    baseURL: process.env.BASE_URL ?? 'http://nginx:80',
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
  ],
});
