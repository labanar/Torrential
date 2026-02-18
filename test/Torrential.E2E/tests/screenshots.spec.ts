import { test, expect, type Page } from '@playwright/test';
import path from 'path';
import fs from 'fs';

const screenshotDir = process.env.SCREENSHOT_DIR ?? '/app/screenshots';
const fixturesDir = path.join(__dirname, '..', 'fixtures');
const desktopProjectName = 'screenshots';

function screenshotPath(projectName: string, screenshotName: string) {
  const suffix = projectName === desktopProjectName ? '' : `-${projectName}`;
  return path.join(screenshotDir, `${screenshotName}${suffix}.png`);
}

async function assertNoHorizontalOverflow(page: Page) {
  const layout = await page.evaluate(() => ({
    documentScrollWidth: document.documentElement.scrollWidth,
    bodyScrollWidth: document.body.scrollWidth,
    viewportWidth: window.innerWidth,
  }));

  expect(layout.documentScrollWidth).toBeLessThanOrEqual(layout.viewportWidth + 1);
  expect(layout.bodyScrollWidth).toBeLessThanOrEqual(layout.viewportWidth + 1);
}

async function expectFullyInViewport(
  page: Page,
  selector: string,
  description: string,
) {
  const viewport = page.viewportSize();
  expect(viewport).not.toBeNull();

  const box = await page.locator(selector).first().boundingBox();
  expect(box, `${description} should be visible`).not.toBeNull();
  expect(box!.x, `${description} should not be clipped on the left`).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width, `${description} should not be clipped on the right`).toBeLessThanOrEqual(
    viewport!.width + 1,
  );
}

async function addTorrentViaApi(baseURL: string, torrentPath: string) {
  const fileBuffer = fs.readFileSync(torrentPath);
  const boundary = '----FormBoundary' + Math.random().toString(36).slice(2);
  const fileName = path.basename(torrentPath);

  const body = Buffer.concat([
    Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="file"; filename="${fileName}"\r\nContent-Type: application/x-bittorrent\r\n\r\n`),
    fileBuffer,
    Buffer.from(`\r\n--${boundary}--\r\n`),
  ]);

  const res = await fetch(`${baseURL}/torrents/add`, {
    method: 'POST',
    headers: { 'Content-Type': `multipart/form-data; boundary=${boundary}` },
    body,
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Failed to add torrent: ${res.status} ${text}`);
  }

  return res.json();
}

test.describe('Screenshots', () => {
  test('home page with torrent', async ({ page, baseURL }, testInfo) => {
    // Add debian torrent via API so the list has data
    const torrentFile = path.join(fixturesDir, 'debian-12.0.0-amd64-netinst.iso.torrent');
    await addTorrentViaApi(baseURL!, torrentFile);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await assertNoHorizontalOverflow(page);

    const torrentRow = page.locator('[class*="torrentContainer"]').filter({ hasText: 'debian' }).first();
    await expect(torrentRow).toBeVisible();
    await expectFullyInViewport(page, '[class*="torrentContainer"]', 'torrent list row');

    await page.screenshot({
      path: screenshotPath(testInfo.project.name, 'home'),
      fullPage: true,
    });
  });

  test('torrent info pane', async ({ page }, testInfo) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Click on the torrent row to open the info pane
    const torrentRow = page.locator('[class*="torrentContainer"]').filter({ hasText: 'debian' });
    await torrentRow.click();

    // Switch to the BITFIELD tab and wait for piece data to load
    await page.locator('text=BITFIELD').click();
    await page.waitForSelector('text=Pieces');
    await assertNoHorizontalOverflow(page);
    await expectFullyInViewport(page, 'text=BITFIELD', 'BITFIELD tab control');
    await expectFullyInViewport(page, 'text=Pieces', 'bitfield content heading');

    await page.screenshot({
      path: screenshotPath(testInfo.project.name, 'torrent-info-pane'),
      fullPage: true,
    });
  });

  test('settings page', async ({ page }, testInfo) => {
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');
    await assertNoHorizontalOverflow(page);

    const settingsSaveAction = page
      .locator('button:has-text("Save"), button:has-text("SAVE"), button:has-text("Save Settings")')
      .first();
    await expect(settingsSaveAction).toBeVisible();
    await expectFullyInViewport(
      page,
      'button:has-text("Save"), button:has-text("SAVE"), button:has-text("Save Settings")',
      'settings save action',
    );

    await page.screenshot({
      path: screenshotPath(testInfo.project.name, 'settings'),
      fullPage: true,
    });
  });
});
