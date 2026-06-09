import { Route, Routes, Navigate } from 'react-router-dom';
import { Layout } from './components/Layout';
import { MattersPage } from './pages/MattersPage';
import { HomePage } from './pages/HomePage';

// Foundations epic ships the shell + the home + an empty matters list page.
// Later epics (Matters, Documents, Calendar, Billing, Trust, Portal, AI, Connectors, Reports)
// add their own route segments inside <Layout>.
export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<HomePage />} />
        <Route path="matters" element={<MattersPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
