import { apiClient } from './api';
import { HealthStatus } from '../types/health';

export const healthService = {
  getStatus: async (): Promise<HealthStatus> => {
    const response = await apiClient.get<HealthStatus>('/health');
    return response.data;
  },

  checkAvailability: async (): Promise<boolean> => {
    try {
      const response = await apiClient.get('/health', { timeout: 3000 });
      return response.status === 200;
    } catch {
      return false;
    }
  },
};
