import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The browser calls /api on this dev server. Vite forwards that to the API
// and strips the prefix, so /api/auth/login becomes http://localhost:5080/auth/login.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
