import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

const screenshotDir = process.env.SCREENSHOT_DIR ?? '/app/screenshots';
const fixturesDir = path.join(__dirname, '..', 'fixtures');

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
  test('home page with torrent', async ({ page, baseURL }) => {
    // Add debian torrent via API so the list has data
    const torrentFile = path.join(fixturesDir, 'debian-12.0.0-amd64-netinst.iso.torrent');
    await addTorrentViaApi(baseURL!, torrentFile);

    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.screenshot({
      path: path.join(screenshotDir, 'home.png'),
      fullPage: true,
    });
  });

  test('torrent info pane', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Click on the torrent row to open the info pane
    const torrentRow = page.locator('[class*="torrentContainer"]').filter({ hasText: 'debian' });
    await torrentRow.click();

    // Switch to the BITFIELD tab and wait for piece data to load
    await page.locator('text=BITFIELD').click();
    await page.waitForSelector('text=Pieces');
    await page.screenshot({
      path: path.join(screenshotDir, 'torrent-info-pane.png'),
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
