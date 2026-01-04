import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect } from 'react';
import { signalRService } from './services/signalr';
import Dashboard from './pages/Dashboard';
import DeviceDetail from './pages/DeviceDetail';
import './App.css';

const queryClient = new QueryClient();

function App() {
  useEffect(() => {
    // Connect to SignalR when app loads
    signalRService.connect();

    return () => {
      // Disconnect when app unmounts
      signalRService.disconnect();
    };
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="min-h-screen bg-gray-50">
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
          </Routes>
        </div>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
