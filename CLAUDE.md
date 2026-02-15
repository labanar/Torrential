# Torrential — Project Instructions

## E2E Screenshot Verification

Every UI change must be visually verified using the Playwright screenshot pipeline before being considered complete. This applies to new pages, layout changes, component additions, and styling updates.

### How It Works

Three Docker containers orchestrated by `docker-compose.yml`:

- **api** — `Torrential.Api` (.NET 10), serves the backend on port 8080
- **nginx** — Builds the React frontend, serves static files, proxies `/api/*` to the API
- **playwright** — Runs headless Chromium against the nginx container, saves screenshots to the host

### Running Screenshots

```bash
# Build everything and capture screenshots (exits when done)
docker compose up --build --abort-on-container-exit playwright

# Screenshots are saved to:
# test/Torrential.E2E/screenshots/*.png
```

To run just the app for manual testing:
```bash
docker compose up --build api nginx
# App available at http://localhost:8080
```

Tear down:
```bash
docker compose down
```

### When to Run

Run the screenshot pipeline after any change that touches UI:
- Adding or modifying a page/route
- Changing layout, sidebar, or navigation
- Updating component styling or structure
- Modifying API responses that affect what the UI renders

### Adding Screenshots for New Pages

When you add a new route to the frontend (`src/torrential-frontend/src/App.tsx`), add a corresponding test in `test/Torrential.E2E/tests/screenshots.spec.ts`:

```ts
test('my new page', async ({ page }) => {
  await page.goto('/my-new-route');
  await page.waitForLoadState('networkidle');
  await page.screenshot({
    path: path.join(screenshotDir, 'my-new-page.png'),
    fullPage: true,
  });
});
```

### Key Files

| File | Purpose |
|---|---|
| `docker-compose.yml` | Orchestrates api, nginx, and playwright containers |
| `src/Torrential.Api/Dockerfile` | Multi-stage .NET build for the API |
| `src/torrential-frontend/Dockerfile` | Multi-stage frontend build + nginx serving |
| `src/torrential-frontend/nginx.conf` | Static file serving + `/api/*` reverse proxy |
| `test/Torrential.E2E/playwright.config.ts` | Playwright config (1280x720 viewport) |
| `test/Torrential.E2E/tests/screenshots.spec.ts` | Screenshot test specs |

### Visual Feedback Loop

After any UI change, you MUST run the screenshot pipeline and visually inspect the results before considering the task complete. This is the standard workflow:

1. Make the UI change (code edits)
2. Run `docker compose up --build --abort-on-container-exit playwright`
3. Read each screenshot PNG from `test/Torrential.E2E/screenshots/` using the Read tool
4. Visually verify the rendered output — check for:
   - Blank pages or error states
   - Broken layout or missing sidebar/navigation
   - Components not rendering or rendering incorrectly
   - Styling issues (wrong colors, misaligned elements, overflow)
   - New UI elements appearing as intended
5. If something looks wrong, fix the code and re-run from step 2
6. Repeat until the screenshots confirm the UI is correct

This loop replaces manual browser testing. Do not skip it for UI work.

Screenshots are gitignored — they are ephemeral build artifacts, not checked in.

## Project Structure

```
src/
  Torrential.Api/          — Minimal API (.NET 10), depends on Application + Core
  Torrential.Application/  — Application layer (services, managers)
  Torrential.Core/         — Domain layer (torrents, trackers, protocols)
  torrential-frontend/     — React + Vite + Tailwind SPA
test/
  Torrential.E2E/          — Playwright screenshot tests (Docker-based)
```

## Dev Proxy

The Vite dev server (`npm run dev` in `torrential-frontend/`) proxies `/api/*` to `http://localhost:5200`, stripping the `/api` prefix. The nginx config in Docker mirrors this behavior, proxying to the `api` container instead.
