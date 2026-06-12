import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/ui/',
  build: {
    outDir: '../virtua-agent-api/wwwroot/ui',
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      '/v1': 'http://localhost:5099',
      '/swagger': 'http://localhost:5099'
    }
  }
});
