# Torrential UI (Vite)

This is the **active** frontend for Torrential, built with React + TypeScript + Vite.

> **Source of truth:** `src/torrential-ui-vite` is the only frontend that ships with the app.
> The other UI directories (`torrential-ui`, `torrential-remix-ui`) are legacy/experimental and are **not** served by `Torrential.Web`.

## Development

```bash
cd src/torrential-ui-vite
npm install
npm run dev
```

The Vite dev server runs on `http://localhost:5173` and proxies API calls to the .NET backend.

## Production Build

Vite is configured to output directly into `src/Torrential.Web/wwwroot`. There are two ways to trigger a build:

### Via npm (standalone)

```bash
cd src/torrential-ui-vite
npm run build
```

### Via dotnet (integrated)

```bash
cd src/Torrential.Web
dotnet build
```

The `.csproj` includes MSBuild targets (`NpmInstall` and `ViteBuild`) that automatically run `npm install` and `npm run build` before the .NET build. To skip the frontend build (e.g., during backend-only iteration), set the environment variable:

```bash
SKIP_VITE_BUILD=true dotnet build
```

### Build output

The build produces hashed assets in `wwwroot/`:

```
src/Torrential.Web/wwwroot/
  index.html          # Entry point (references hashed JS/CSS)
  vite.svg            # Favicon
  assets/
    index-<hash>.js   # Bundled application code
    index-<hash>.css  # Bundled styles
```

These files are **not checked into git** (see `.gitignore`). They are generated at build time.

## PR Verification Checklist

Before merging frontend changes, verify:

- [ ] `npm run build` in `src/torrential-ui-vite` succeeds without errors
- [ ] `dotnet build` in `src/Torrential.Web` succeeds (this runs the Vite build automatically)
- [ ] Run the app with `dotnet run` and confirm `wwwroot/index.html` loads in the browser
- [ ] If the change affects the split pane or layout, click a torrent in the list to verify the detail pane renders
- [ ] Check browser DevTools Network tab: asset requests (JS/CSS) return 200, not 404

## Shadcn Migration Notes (Phase 4)

- Chakra UI and Emotion dependencies were removed from `src/torrential-ui-vite/package.json`.
- Shared UI primitives are now provided by local shadcn-based components under `src/components/ui`.
- Theme switching is handled by toggling the `dark` class on `document.documentElement` and persisting `theme` in `localStorage`.

### Intentional UI Differences

- Dialog, button, checkbox, tooltip, and input visuals now follow shadcn/Tailwind styling rather than Chakra defaults.
- Spacing, focus rings, and hover/active states may differ slightly from prior Chakra rendering while preserving behavior.
- The app uses Sonner-based toast rendering; toast visuals are intentionally different from Chakra's toast UI.
