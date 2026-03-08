# Torrential

## UI Screenshots

To take UI screenshots for visual verification, run the Playwright UI screenshot tests:

```bash
cd test/Torrential.E2E && npx playwright test --project=ui-desktop ui-screenshots.spec.ts
```

Screenshots are saved to `test/Torrential.E2E/screenshots/`. The dev server at localhost:5173 is started automatically.

Use these screenshots to verify UI changes look correct before considering work done. Review the dark mode screenshots in particular since the app defaults to dark mode.

## Project Structure

- `src/torrential-ui-vite/` - Vite + React frontend
- `test/Torrential.E2E/` - Playwright E2E and screenshot tests
- `test/Torrential.E2E/tests/ui-screenshots.spec.ts` - UI screenshot test specs

## Dev Commands

- **Frontend dev server**: `cd src/torrential-ui-vite && npm run dev` (port 5173)
- **UI screenshots (empty state)**: `cd test/Torrential.E2E && npx playwright test --project=ui-desktop ui-screenshots.spec.ts`
- **UI screenshots (with mock data)**: `cd test/Torrential.E2E && npx playwright test --project=ui-desktop detail-pane-screenshot.spec.ts`
- **All UI screenshots**: `cd test/Torrential.E2E && npx playwright test --project=ui-desktop`

## Screenshot Verification

After making UI changes, take screenshots and review them visually using the Read tool on the PNG files in `test/Torrential.E2E/screenshots/`. The `detail-pane-screenshot.spec.ts` test mocks API responses to render torrents with the detail pane open — use this to verify components that require data (progress bars, peer lists, etc.).
