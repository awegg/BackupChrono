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

/**
 * Format a timestamp to relative time if <48h, absolute otherwise
 * Shows hover tooltip with both formats
 */
export function formatTimestampSmart(date: Date | null): string {
  if (!date) {
    return 'Never backed up';
  }

  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffHours = diffMs / (1000 * 60 * 60);

  // If less than 48 hours, show relative time
  if (diffHours < 48 && diffHours >= 0) {
    if (diffHours < 1) {
      const diffMinutes = Math.floor(diffMs / (1000 * 60));
      if (diffMinutes < 1) {
        return 'just now';
      }
      return `${diffMinutes} min${diffMinutes === 1 ? '' : 's'} ago`;
    }
    const hours = Math.floor(diffHours);
    return `${hours} hour${hours === 1 ? '' : 's'} ago`;
  }

  // If less than 7 days, show days
  if (diffHours < 168 && diffHours >= 0) {
    const days = Math.floor(diffHours / 24);
    return `${days} day${days === 1 ? '' : 's'} ago`;
  }

  // Otherwise show absolute timestamp
  return date.toLocaleString('en-US', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}

/**
 * Get absolute timestamp for tooltip
 */
export function getAbsoluteTimestamp(date: Date | null): string {
  if (!date) {
    return 'No backup recorded';
  }

  return date.toLocaleString('en-US', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

/**
 * Format file size in GB
 */
export function formatSize(sizeGB: number): string {
  return `${sizeGB.toFixed(1)} GB`;
}

/**
 * Format file count with commas
 */
export function formatFileCount(count: number): string {
  return count.toLocaleString();
}
