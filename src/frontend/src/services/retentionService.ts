import { apiClient } from './api';
import { RetentionLastRun, RetentionRunResult } from '../types';

export const retentionService = {
  async getLastRun(): Promise<RetentionLastRun | null> {
    const response = await apiClient.get<RetentionLastRun | null>('/api/retention-policy/last-run');
    return response.data;
  },

  async runRetention(dryRun: boolean = false): Promise<RetentionRunResult[]> {
    const response = await apiClient.post<RetentionRunResult[]>(
      '/api/retention-policy/execute',
      undefined,
      { 
        params: { dryRun },
        timeout: 300000 // 5 minutes for retention operations (restic prune can be slow)
      }
    );
    return response.data;
  },
};
