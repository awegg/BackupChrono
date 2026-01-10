import { useState } from 'react';
import { AlertCircle, ChevronDown, ChevronRight } from 'lucide-react';

interface ErrorDisplayProps {
  error: unknown;
}

export function ErrorDisplay({ error }: ErrorDisplayProps) {
  const [showDetails, setShowDetails] = useState(false);

  // Extract error information
  const errorMessage = (() => {
    if (typeof error === 'string') return error;
    if (error && typeof error === 'object') {
      const maybeResp = (error as { response?: { data?: unknown } }).response;
      const data = maybeResp?.data as { Error?: string; error?: string } | undefined;
      const message = (error as { message?: string }).message;
      return data?.Error || data?.error || message || 'An error occurred';
    }
    return 'An error occurred';
  })();
  
  const hasDetails = typeof error === 'object' && error !== null;
  const statusCode = hasDetails && (error as { response?: { status?: number } }).response?.status;
  const responseData = hasDetails && (error as { response?: { data?: unknown } }).response?.data;
  const stack = (error as { stack?: string } | null | undefined)?.stack;

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
                
                {responseData !== null && responseData !== undefined && (
                  <div>
                    <dt className="font-semibold text-red-900">Response:</dt>
                    <dd className="text-red-700 ml-4">
                      <pre className="bg-red-100 p-2 rounded overflow-x-auto whitespace-pre-wrap">
                        {typeof responseData === 'object' ? JSON.stringify(responseData, null, 2) : String(responseData)}
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
                
                {hasDetails && (error as { config?: { url?: string } }).config?.url && (
                  <div>
                    <dt className="font-semibold text-red-900">Request URL:</dt>
                    <dd className="text-red-700 ml-4 break-all">{(error as { config?: { url?: string } }).config?.url}</dd>
                  </div>
                )}
                
                {(() => {
                  const configMethod = hasDetails && (error as { config?: { method?: unknown } }).config?.method;
                  if (!configMethod) return null;
                  const methodStr = typeof configMethod === 'string' ? configMethod.toUpperCase() : String(configMethod);
                  return (
                    <div>
                      <dt className="font-semibold text-red-900">Request Method:</dt>
                      <dd className="text-red-700 ml-4">{methodStr}</dd>
                    </div>
                  );
                })()}
              </dl>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
