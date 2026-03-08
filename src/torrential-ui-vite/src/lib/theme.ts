export type Theme = "light" | "dark";

const THEME_STORAGE_KEY = "theme";

export function resolveInitialTheme(): Theme {
  if (typeof window === "undefined") {
    return "light";
  }

  const savedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
  if (savedTheme === "light" || savedTheme === "dark") {
    return savedTheme;
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

export function applyTheme(theme: Theme): void {
  const root = document.documentElement;
  root.classList.toggle("dark", theme === "dark");
  root.style.colorScheme = theme;
  window.localStorage.setItem(THEME_STORAGE_KEY, theme);
}

export function applyInitialTheme(): Theme {
  const initialTheme = resolveInitialTheme();
  applyTheme(initialTheme);
  return initialTheme;
}
