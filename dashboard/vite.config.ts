import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  build: {
    // Built assets are served directly by the Portix.Client daemon's static-file middleware.
    outDir: '../src/Portix.Client/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      // During `npm run dev`, proxy API/hub calls to the daemon so the dev server can be used standalone.
      '/api': 'http://127.0.0.1:4040',
      '/hubs': { target: 'http://127.0.0.1:4040', ws: true },
    },
  },
})
