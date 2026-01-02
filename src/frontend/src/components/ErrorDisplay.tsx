import { useState } from 'react';
import { AlertCircle, ChevronDown, ChevronRight } from 'lucide-react';

interface ErrorDisplayProps {
  error: any;
}

export function ErrorDisplay({ error }: ErrorDisplayProps) {
  const [showDetails, setShowDetails] = useState(false);

  // Extract error information
  const errorMessage = typeof error === 'string' 
    ? error 
    : error?.response?.data?.Error || error?.response?.data?.error || error?.message || 'An error occurred';
  
  const hasDetails = typeof error === 'object' && error !== null;
  const statusCode = error?.response?.status;
  const responseData = error?.response?.data;
  const stack = error?.stack;

  return (
    <div className="bg-red-50 border border-red-400 rounded-md p-4">
      <div className="flex items-start">
        <AlertCircle className="h-5 w-5 text-red-600 mt-0.5 mr-3 flex-shrink-0" />
        <div className="flex-1">
          <div className="flex items-center justify-between">
            <p className="text-sm font-medium text-red-800">{errorMessage}</p>
            {hasDetails && (
              <button
                type="button"
                onClick={() => setShowDetails(!showDetails)}
                className="flex items-center text-xs text-red-700 hover:text-red-900 ml-4"
              >
                {showDetails ? (
                  <>
                    <ChevronDown className="h-4 w-4 mr-1" />
                    Hide details
                  </>
                ) : (
                  <>
                    <ChevronRight className="h-4 w-4 mr-1" />
                    Show details
                  </>
                )}
              </button>
            )}
          </div>
          
          {showDetails && hasDetails && (
            <div className="mt-3 pt-3 border-t border-red-300">
              <dl className="space-y-2 text-xs">
                {statusCode && (
                  <div>
                    <dt className="font-semibold text-red-900">Status Code:</dt>
                    <dd className="text-red-700 ml-4">{statusCode}</dd>
                  </div>
                )}
                
                {responseData && (
                  <div>
                    <dt className="font-semibold text-red-900">Response:</dt>
                    <dd className="text-red-700 ml-4">
                      <pre className="bg-red-100 p-2 rounded overflow-x-auto whitespace-pre-wrap">
                        {JSON.stringify(responseData, null, 2)}
                      </pre>
                    </dd>
                  </div>
                )}
                
                {stack && (
                  <div>
                    <dt className="font-semibold text-red-900">Stack Trace:</dt>
                    <dd className="text-red-700 ml-4">
                      <pre className="bg-red-100 p-2 rounded overflow-x-auto text-xs whitespace-pre-wrap font-mono">
                        {stack}
                      </pre>
                    </dd>
                  </div>
                )}
                
                {error?.config?.url && (
                  <div>
                    <dt className="font-semibold text-red-900">Request URL:</dt>
                    <dd className="text-red-700 ml-4 break-all">{error.config.url}</dd>
                  </div>
                )}
                
                {error?.config?.method && (
                  <div>
                    <dt className="font-semibold text-red-900">Request Method:</dt>
                    <dd className="text-red-700 ml-4">{error.config.method.toUpperCase()}</dd>
                  </div>
                )}
              </dl>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
