export interface HealthStatus {
  status: string; // "Healthy", "Degraded", "Unhealthy"
  timestamp: string;
  version: string;
  uptime: string;
  checks: HealthCheck[];
}

export interface HealthCheck {
  name: string;
  status: string; // "Healthy", "Warning", "Critical"
  message: string;
  details?: Record<string, any>;
}
