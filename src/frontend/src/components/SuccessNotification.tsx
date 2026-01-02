import { useEffect } from 'react';
import { CheckCircle, X } from 'lucide-react';

interface SuccessNotificationProps {
  message: string;
  onClose: () => void;
  autoCloseMs?: number;
}

export function SuccessNotification({ message, onClose, autoCloseMs = 3000 }: SuccessNotificationProps) {
  useEffect(() => {
    if (autoCloseMs > 0) {
      const timer = setTimeout(onClose, autoCloseMs);
      return () => clearTimeout(timer);
    }
  }, [autoCloseMs, onClose]);

  return (
    <div className="fixed top-4 right-4 z-50 animate-fade-in">
      <div className="bg-green-50 border border-green-400 rounded-md p-4 shadow-lg max-w-md">
        <div className="flex items-start">
          <CheckCircle className="h-5 w-5 text-green-600 mt-0.5 mr-3 flex-shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-green-800">{message}</p>
          </div>
          <button
            onClick={onClose}
            className="ml-4 text-green-600 hover:text-green-800"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
