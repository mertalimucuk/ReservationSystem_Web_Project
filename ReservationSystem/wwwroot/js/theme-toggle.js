// wwwroot/js/theme-toggle.js

(() => {
  "use strict";

  const THEME_KEY = "themePreference";
  const THEME_DARK = "dark";
  const THEME_LIGHT = "light";

  const themeToggleBtn = document.getElementById("themeToggleBtn");
  const moonIcon = document.getElementById("themeIconMoon");
  const sunIcon = document.getElementById("themeIconSun");
  const htmlElement = document.documentElement;

  const applyStoredTheme = () => {
    const storedTheme = localStorage.getItem(THEME_KEY);
    if (storedTheme) {
      setTheme(storedTheme);
    } else {
      const prefersDark = window.matchMedia(
        "(prefers-color-scheme: dark)"
      ).matches;
      setTheme(prefersDark ? THEME_DARK : THEME_LIGHT);
    }
  };

  const setTheme = (theme) => {
    if (theme === THEME_DARK) {
      htmlElement.setAttribute("data-bs-theme", THEME_DARK);
      if (moonIcon) moonIcon.classList.add("d-none");
      if (sunIcon) sunIcon.classList.remove("d-none");
      localStorage.setItem(THEME_KEY, THEME_DARK);
    } else {
      htmlElement.setAttribute("data-bs-theme", THEME_LIGHT);
      if (moonIcon) moonIcon.classList.remove("d-none");
      if (sunIcon) sunIcon.classList.add("d-none");
      localStorage.setItem(THEME_KEY, THEME_LIGHT);
    }
  };

  // Butona tıklama olayını ekle
  if (themeToggleBtn) {
    themeToggleBtn.addEventListener("click", () => {
      const currentTheme = htmlElement.getAttribute("data-bs-theme");
      setTheme(currentTheme === THEME_DARK ? THEME_LIGHT : THEME_DARK);
    });
  }

  // Sistem tema değişikliğini dinle
  window
    .matchMedia("(prefers-color-scheme: dark)")
    .addEventListener("change", (event) => {
      if (!localStorage.getItem(THEME_KEY)) {
        // Sadece kullanıcı özellikle bir tema seçmemişse sistem değişikliğine uy
        setTheme(event.matches ? THEME_DARK : THEME_LIGHT);
      }
    });

  // İlk yüklemede temayı uygula
  applyStoredTheme();
})();
