import React from 'react';
import { FileEntry } from '../types';
import { Folder, File, Download, ChevronRight, Home } from 'lucide-react';

interface FileBrowserProps {
  backupId: string;
  files: FileEntry[];
  currentPath: string;
  onNavigate: (path: string) => void;
  onDownload?: (file: FileEntry) => void;
  loading?: boolean;
}

export const FileBrowser: React.FC<FileBrowserProps> = ({
  files,
  currentPath,
  onNavigate,
  onDownload,
  loading
}) => {
  const formatSize = (bytes: number) => {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;
    
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    
    return `${size.toFixed(2)} ${units[unitIndex]}`;
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  const handleFileClick = (file: FileEntry) => {
    if (file.isDirectory) {
      onNavigate(file.path);
    }
  };

  const handleDownloadClick = (e: React.MouseEvent, file: FileEntry) => {
    e.stopPropagation();
    onDownload?.(file);
  };

  // Build breadcrumb path segments
  const getBreadcrumbs = () => {
    const normalizedPath = currentPath.replace(/\\/g, '/').replace(/\/+/g, '/');
    if (normalizedPath === '/' || normalizedPath === '') {
      return [{ label: 'Root', path: '/' }];
    }

    const segments = normalizedPath.split('/').filter(s => s);
    const breadcrumbs = [{ label: 'Root', path: '/' }];
    
    let currentBreadcrumbPath = '';
    for (const segment of segments) {
      currentBreadcrumbPath += '/' + segment;
      breadcrumbs.push({
        label: segment,
        path: currentBreadcrumbPath
      });
    }
    
    return breadcrumbs;
  };

  const breadcrumbs = getBreadcrumbs();

  return (
    <div className="bg-white rounded-lg shadow">
      {/* Breadcrumb Navigation */}
      <div className="border-b border-gray-200 p-4">
        <div className="flex items-center space-x-2 text-sm">
          <Home size={16} className="text-gray-400" />
          {breadcrumbs.map((crumb, index) => (
            <React.Fragment key={crumb.path}>
              {index > 0 && <ChevronRight size={14} className="text-gray-400" />}
              <button
                onClick={() => onNavigate(crumb.path)}
                className={`hover:text-blue-600 ${
                  index === breadcrumbs.length - 1
                    ? 'font-semibold text-gray-900'
                    : 'text-gray-600'
                }`}
                disabled={loading}
              >
                {crumb.label}
              </button>
            </React.Fragment>
          ))}
        </div>
      </div>

      {/* File List */}
      <div className="overflow-x-auto">
        {loading ? (
          <div className="flex justify-center items-center p-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
          </div>
        ) : files.length === 0 ? (
          <div className="text-center p-8 text-gray-500">
            <Folder className="mx-auto mb-4 text-gray-400" size={48} />
            <p>This folder is empty</p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Name
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Size
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Modified
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {files.map((file) => (
                <tr
                  key={file.path}
                  className={`${file.isDirectory ? 'cursor-pointer hover:bg-gray-50' : ''}`}
                  onClick={() => handleFileClick(file)}
                >
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="flex items-center space-x-2">
                      {file.isDirectory ? (
                        <Folder className="text-blue-500" size={20} />
                      ) : (
                        <File className="text-gray-400" size={20} />
                      )}
                      <span className="text-sm font-medium text-gray-900">
                        {file.name}
                      </span>
                    </div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                    {file.isDirectory ? '-' : formatSize(file.size)}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                    {formatDate(file.modifiedAt)}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm">
                    {!file.isDirectory && onDownload && (
                      <button
                        onClick={(e) => handleDownloadClick(e, file)}
                        className="inline-flex items-center px-3 py-1 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
                      >
                        <Download size={14} className="mr-1" />
                        Download
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};
