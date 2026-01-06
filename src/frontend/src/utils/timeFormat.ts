/**
 * Formats a timestamp into a human-readable relative time string.
 * 
 * @param timestamp - ISO 8601 timestamp string
 * @returns Human-readable relative time (e.g., "5 minutes ago", "2 hours ago", "3 days ago")
 */
export function formatTimestamp(timestamp: string): string {
  if (!timestamp) {
    return 'Unknown';
  }

  const date = new Date(timestamp);
  if (isNaN(date.getTime())) {
    return 'Invalid date';
  }

  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  
  // Handle future timestamps
  if (diffMs < 0) {
    return 'in the future';
  }

  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins === 0) return 'just now';
  if (diffMins === 1) return '1 minute ago';
  if (diffMins < 60) return `${diffMins} minutes ago`;
  if (diffHours === 1) return '1 hour ago';
  if (diffHours < 24) return `${diffHours} hours ago`;
  if (diffDays === 1) return '1 day ago';
  return `${diffDays} days ago`;
}
