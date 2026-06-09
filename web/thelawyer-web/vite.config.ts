import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import path from 'node:path';

// Per ARCH.md: PWA via vite-plugin-pwa (offline manifest + service worker).
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      manifest: {
        name: 'TheLawyer',
        short_name: 'TheLawyer',
        description: 'Multi-tenant practice management for US small law firms',
        theme_color: '#0a0a0a',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: '/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png' },
        ],
      },
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: Number(process.env.VITE_PORT) || 5173,
    strictPort: false,
    proxy: {
      // Forward API calls to the Aspire-orchestrated TheLawyer.Api container.
      '/api': {
        target: process.env.services__api__http__0 ?? 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
});
