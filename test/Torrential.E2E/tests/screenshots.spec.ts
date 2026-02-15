import { test } from '@playwright/test';
import path from 'path';

const screenshotDir = process.env.SCREENSHOT_DIR ?? '/app/screenshots';

test.describe('Screenshots', () => {
  test('home page', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.screenshot({
      path: path.join(screenshotDir, 'home.png'),
      fullPage: true,
    });
  });

  test('settings page', async ({ page }) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');
    await page.screenshot({
      path: path.join(screenshotDir, 'settings.png'),
      fullPage: true,
    });
  });
});
