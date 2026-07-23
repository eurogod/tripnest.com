/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Dev proxy: the TripNest.Core backend (http://localhost:5091) only allows
// configured CORS origins, so we serve the API same-origin and let Vite
// forward. Production builds should set VITE_API_URL instead.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // Pinned: Core's CORS allowlist (and the direct SignalR chat connection)
    // expects this exact origin — silently drifting to 5174+ when 5173 is
    // busy breaks auth'd calls in confusing ways. trip-card runs on 5250.
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5091',
      '/health': 'http://localhost:5091',
      '/hubs': { target: 'http://localhost:5091', ws: true },
      // Backend-served media (listing photos, walkthrough videos).
      '/uploads': 'http://localhost:5091',
      // Veo (Gemini API) — same-origin in dev so the browser avoids CORS.
      '/veo': {
        target: 'https://generativelanguage.googleapis.com',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/veo/, ''),
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
