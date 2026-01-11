export interface LogEntry {
  timestamp: string;
  level: string;
  message: string;
  exception?: string;
  sourceContext?: string;
}

export interface LogQueryParameters {
  level?: string;
  search?: string;
  startDate?: string;
  endDate?: string;
  limit?: number;
}
