import { Navigate, Route, Routes } from 'react-router-dom';
import LoginPage from './features/auth/LoginPage';
import AuthGuard from './features/auth/AuthGuard';
import PortfolioPage from './features/portfolio/PortfolioPage';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/portfolio"
        element={
          <AuthGuard>
            <PortfolioPage />
          </AuthGuard>
        }
      />
      <Route path="*" element={<Navigate to="/portfolio" replace />} />
    </Routes>
  );
}
