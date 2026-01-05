import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { signalRService } from './services/signalr';
import Dashboard from './pages/Dashboard';
import DeviceDetail from './pages/DeviceDetail';
import { BackupsListPage } from './pages/BackupsList';
import { DevicesPage } from './pages/DevicesPage';
import { FileBrowserPage } from './pages/FileBrowserPage';
import { ErrorNotification } from './components/ErrorNotification';
import { Sidebar } from './components/Sidebar';
import './App.css';

const queryClient = new QueryClient();

function App() {
  const [signalRError, setSignalRError] = useState<string | null>(null);

  useEffect(() => {
    // Set initial theme class (dark by default)
    const theme = localStorage.getItem('theme') || 'dark';
    document.documentElement.classList.add(theme);

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
        <div className="min-h-screen bg-background text-foreground flex">
          {/* Sidebar */}
          <Sidebar />

          {/* Main Content */}
          <div className="flex-1 ml-64 min-h-screen bg-background">
            {signalRError && (
              <ErrorNotification 
                message={signalRError} 
                onClose={() => setSignalRError(null)}
              />
            )}
            
            <main className="p-8 min-h-screen">
              <Routes>
                <Route path="/" element={<Dashboard />} />
                <Route path="/devices" element={<DevicesPage />} />
                <Route path="/devices/:deviceId" element={<DeviceDetail />} />
                <Route path="/devices/:deviceId/backups" element={<BackupsListPage />} />
                <Route path="/devices/:deviceId/backups/:backupId/browse" element={<FileBrowserPage />} />
                <Route path="/backups/:backupId/browse" element={<FileBrowserPage />} />
              </Routes>
            </main>
          </div>
        </div>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
