import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/app/',
  build: {
    outDir: '../virtua-agent-api/VirtuaAgent.Api/wwwroot/app',
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
