import { Navigate, useLocation, useParams } from 'react-router-dom';

export function LegacyFilesRedirect() {
  const { backupId } = useParams<{ backupId: string }>();
  const location = useLocation();
  const search = location.search || '';

  const target = `/backups/${backupId}/browse${search}`;
  return <Navigate to={target} replace />;
}
