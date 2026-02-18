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

async function getElementHeight(page: Page, selector: string) {
  const box = await page.locator(selector).first().boundingBox();
  expect(box, `${selector} should be visible`).not.toBeNull();
  return box!.height;
}

async function expectPaneHeightStableAcrossTabs(page: Page, tolerancePx: number) {
  const paneSelector = '[class*="bottomPane"]';
  const peersHeight = await getElementHeight(page, paneSelector);

  await page.locator('button:has-text("BITFIELD")').click();
  await page.waitForSelector('text=Pieces');
  const bitfieldHeight = await getElementHeight(page, paneSelector);

  await page.locator('button:has-text("FILES")').click();
  await expect(
    page.locator('text=Filename').or(page.locator('text=No files')).first(),
  ).toBeVisible();
  const filesHeight = await getElementHeight(page, paneSelector);

  const heights = [peersHeight, bitfieldHeight, filesHeight];
  const minHeight = Math.min(...heights);
  const maxHeight = Math.max(...heights);
  expect(maxHeight - minHeight).toBeLessThanOrEqual(tolerancePx);
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
    await assertNoHorizontalOverflow(page);

    const torrentListSelector = '[class*="torrentList"]';
    const detailPaneSelector = '[class*="bottomPane"]';
    const splitterSelector = '[class*="splitPaneHandle"]';
    const detailHeightTolerancePx = testInfo.project.name.includes('mobile') ? 6 : 4;
    const reclaimTolerancePx = 6;

    // Click on the torrent row to open the info pane
    const torrentRow = page.locator('[class*="torrentContainer"]').filter({ hasText: 'debian' });
    const listHeightBeforeOpen = await getElementHeight(page, torrentListSelector);
    await torrentRow.click();
    await page.waitForSelector(detailPaneSelector);

    const listHeightWithPaneOpen = await getElementHeight(page, torrentListSelector);
    expect(listHeightWithPaneOpen).toBeLessThan(listHeightBeforeOpen - 40);

    await expectPaneHeightStableAcrossTabs(page, detailHeightTolerancePx);
    await assertNoHorizontalOverflow(page);

    // Switch to the BITFIELD tab and wait for piece data to load
    await page.locator('button:has-text("BITFIELD")').click();
    await page.waitForSelector('text=Pieces');

    const closeButton = page.locator('button[aria-label="Close torrent details"]');
    await expect(closeButton).toBeVisible();
    await closeButton.click();

    await expect(page.locator(detailPaneSelector)).toHaveCount(0);
    await expect(page.locator(splitterSelector)).toHaveCount(0);
    const listHeightAfterClose = await getElementHeight(page, torrentListSelector);
    expect(Math.abs(listHeightAfterClose - listHeightBeforeOpen)).toBeLessThanOrEqual(reclaimTolerancePx);

    await assertNoHorizontalOverflow(page);
    await expectFullyInViewport(page, '[class*="torrentContainer"]', 'torrent list row');

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

test.describe('Filter input', () => {
  const torrentRowSelector = '[class*="torrentContainer"]';
  const filterInputSelector = 'input[placeholder="Filter"]';
  const emptyStateSelector = '[class*="emptyFilterState"]';

  test.beforeEach(async ({ page, baseURL }) => {
    // Ensure at least one torrent exists so the list is populated
    const torrentFile = path.join(fixturesDir, 'debian-12.0.0-amd64-netinst.iso.torrent');
    await addTorrentViaApi(baseURL!, torrentFile).catch(() => {
      // Torrent may already exist from a prior test; ignore duplicate errors
    });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await expect(page.locator(torrentRowSelector).first()).toBeVisible();
  });

  test('matching filter reduces visible rows', async ({ page }) => {
    const filterInput = page.locator(filterInputSelector);
    const initialRowCount = await page.locator(torrentRowSelector).count();
    expect(initialRowCount).toBeGreaterThan(0);

    // Type a term that matches the debian torrent
    await filterInput.fill('debian');
    await expect(page.locator(torrentRowSelector).first()).toBeVisible();

    const filteredCount = await page.locator(torrentRowSelector).count();
    expect(filteredCount).toBeGreaterThan(0);
    expect(filteredCount).toBeLessThanOrEqual(initialRowCount);

    await assertNoHorizontalOverflow(page);
  });

  test('non-matching filter shows empty state', async ({ page }) => {
    const filterInput = page.locator(filterInputSelector);

    // Type a term that matches nothing
    await filterInput.fill('zzz_no_match_zzz');

    // Torrent rows should disappear
    await expect(page.locator(torrentRowSelector)).toHaveCount(0);

    // Empty-state message should be visible
    const emptyState = page.locator(emptyStateSelector);
    await expect(emptyState).toBeVisible();
    await expect(emptyState).toContainText('No torrents match');

    await assertNoHorizontalOverflow(page);
  });

  test('clearing filter restores all rows', async ({ page }) => {
    const filterInput = page.locator(filterInputSelector);
    const initialRowCount = await page.locator(torrentRowSelector).count();

    // Filter to non-matching, then clear
    await filterInput.fill('zzz_no_match_zzz');
    await expect(page.locator(torrentRowSelector)).toHaveCount(0);

    await filterInput.fill('');
    await expect(page.locator(torrentRowSelector).first()).toBeVisible();

    const restoredCount = await page.locator(torrentRowSelector).count();
    expect(restoredCount).toBe(initialRowCount);

    await assertNoHorizontalOverflow(page);
  });
});
