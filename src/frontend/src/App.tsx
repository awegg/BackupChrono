import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { signalRService } from './services/signalr';
import Dashboard from './pages/Dashboard';
import DeviceDetail from './pages/DeviceDetail';
import { BackupBrowser } from './pages/BackupBrowser';
import { ErrorNotification } from './components/ErrorNotification';
import './App.css';

const queryClient = new QueryClient();

function App() {
  const [signalRError, setSignalRError] = useState<string | null>(null);

  useEffect(() => {
    // Connect to SignalR when app loads
    signalRService.connect().catch((err) => {
      console.error('Failed to connect to SignalR:', err);
      setSignalRError('Failed to connect to real-time updates. Backup progress will not update automatically.');
    });

    return () => {
      // Disconnect when app unmounts
      signalRService.disconnect();
    };
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="min-h-screen bg-gray-50">
          {signalRError && (
            <ErrorNotification 
              message={signalRError} 
              onClose={() => setSignalRError(null)}
            />
          )}
          <nav className="bg-blue-600 text-white shadow-lg">
            <div className="max-w-7xl mx-auto px-4 py-3">
              <Link to="/" className="text-2xl font-bold">
                BackupChrono
              </Link>
            </div>
          </nav>
          
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/devices/:deviceId" element={<DeviceDetail />} />
            <Route path="/devices/:deviceId/backups" element={<BackupBrowser />} />
          </Routes>
        </div>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
