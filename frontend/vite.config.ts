import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// API base for the backend (TripNest.Core). Dev server proxies these paths so the
// browser talks same-origin (no CORS) and SignalR websockets upgrade cleanly.
const API_TARGET = process.env.VITE_API_TARGET ?? 'http://localhost:5091';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': path.resolve(__dirname, 'src') },
  },
  server: {
    port: 3000,
    host: true,
    proxy: {
      '/api': { target: API_TARGET, changeOrigin: true },
      '/uploads': { target: API_TARGET, changeOrigin: true },
      '/health': { target: API_TARGET, changeOrigin: true },
      '/hubs': { target: API_TARGET, changeOrigin: true, ws: true },
    },
  },
});
