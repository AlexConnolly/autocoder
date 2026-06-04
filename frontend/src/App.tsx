import { useEffect } from 'react';
import { Route, Routes, useNavigate } from 'react-router-dom';
import BoardPage from './pages/BoardPage';
import SettingsPage from './pages/SettingsPage';
import { ThemeProvider } from './hooks/useTheme';

export default function App() {
  const navigate = useNavigate();

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === '/' && e.target === document.body) {
        e.preventDefault();
        navigate('/settings');
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [navigate]);

  return (
    <ThemeProvider>
      <Routes>
        <Route path="/" element={<BoardPage />} />
        <Route path="/board/:boardId" element={<BoardPage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Routes>
    </ThemeProvider>
  );
}
