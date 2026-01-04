# BackupChrono Design System

**Created**: January 4, 2026  
**Figma File**: [Design System for Backup Tool](https://www.figma.com/design/D228AAj8oZMJTzwKBP3wG9/Design-System-for-Backup-Tool)

## Overview

Professional IT tool design system optimized for sysadmins monitoring backup operations during 8+ hour sessions. Emphasis on clarity, scannability, and low eye strain.

## Core Principles

1. **Clarity over Beauty** - Information density where needed, whitespace where it helps
2. **Scannable not Readable** - Admins need to spot problems in <5 seconds
3. **Low Eye Strain** - Soft backgrounds (#f5f7fa instead of pure white), proper contrast
4. **Professional Aesthetic** - Clean, minimal, data-focused

## Color Palette

### Status Colors
Critical for instant problem identification:

- **Success**: `#10b981` (Green) - Backups running smoothly
- **Warning**: `#f59e0b` (Amber) - Attention needed, non-critical
- **Error**: `#ef4444` (Red) - Failed backups, immediate action required
- **Neutral**: `#6b7280` (Gray) - Informational, no action needed

Each status color has:
- Base color (for badges, icons)
- Background variant (for cards, alerts)
- Foreground variant (for text on colored backgrounds)

### Interactive Elements
- **Primary**: `#3b82f6` (Blue) - Main CTAs, selected states
- **Secondary**: `#e5e7eb` (Light Gray) - Secondary actions
- **Destructive**: `#ef4444` (Red) - Dangerous actions (delete, stop backup)

### Sidebar (Dark Theme)
- **Background**: `#1e293b` (Slate 800)
- **Foreground**: `#e2e8f0` (Slate 200)
- **Accent**: `#334155` (Slate 700) for hover states

### Backgrounds
- **Main Background**: `#f5f7fa` - Soft, reduces eye strain
- **Card Background**: `#ffffff` - Pure white for content cards
- **Muted**: `#f3f4f6` - Subtle backgrounds for less important content

## Typography

### Font Families
- **Sans**: System font stack (`-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, ...`)
- **Mono**: `SF Mono, Monaco, Cascadia Code, Roboto Mono, Consolas` - For paths, IPs, timestamps

### Type Scale
- **xs**: 0.75rem (12px) - Timestamps, metadata
- **sm**: 0.875rem (14px) - Secondary text, labels
- **base**: 1rem (16px) - Body text, default
- **lg**: 1.125rem (18px) - Subheadings
- **xl**: 1.25rem (20px) - Section titles
- **2xl**: 1.5rem (24px) - Page titles

### Font Weights
- **Normal**: 400 - Body text
- **Medium**: 500 - Headings, labels, buttons

## Spacing System

8px base unit for consistent rhythm:

- **1**: 4px (0.25rem) - Tight spacing
- **2**: 8px (0.5rem) - Base unit
- **3**: 16px (1rem) - Standard spacing
- **4**: 24px (1.5rem) - Section spacing
- **5**: 32px (2rem) - Component spacing
- **6**: 48px (3rem) - Large spacing
- **7**: 64px (4rem) - Page section spacing

## Component Guidelines

### Cards
- **Background**: `--card` (white)
- **Border Radius**: `--radius` (0.5rem / 8px)
- **Shadow**: `--shadow-md` for elevation
- **Padding**: `--spacing-4` (24px) standard

### Buttons
- **Height**: 40px (minimum touch target)
- **Padding**: Horizontal 16px, Vertical 8px
- **Border Radius**: `--radius`
- **States**: Default, Hover (-darker), Active, Disabled (opacity 0.5)

### Status Badges
- **Small pills** with icon + text
- **Height**: 24px
- **Padding**: 4px 8px
- **Border Radius**: `--radius`
- **Colors**: Use status color variants

### Tables
- **Zebra striping** for readability with large datasets
- **Hover state** to highlight row
- **Border**: Subtle `--border` color
- **Cell padding**: 12px vertical, 16px horizontal

### Forms
- **Input height**: 40px
- **Background**: `--input-background`
- **Border**: `--border`
- **Focus ring**: `--input-focus` with 2px offset
- **Validation**: Use status colors for error/success states

### Progress Bars
- **Linear bars** for storage usage, backup progress
- **Height**: 8px for inline, 12px for prominent
- **Background**: `--muted`
- **Fill**: Status color (success for normal, warning for high usage)

## Dark Mode Support

Full dark mode support via `.dark` class:
- Inverted backgrounds (dark slate tones)
- Adjusted status colors for visibility on dark backgrounds
- Deeper shadows for depth perception
- Sidebar remains consistently dark in both modes

## Usage in Code

### With Tailwind Classes
```tsx
<div className="bg-card text-card-foreground shadow-md rounded-lg p-4">
  <h2 className="text-xl font-medium">Device Status</h2>
  <span className="bg-status-success-bg text-status-success-fg px-2 py-1 rounded text-sm">
    Healthy
  </span>
</div>
```

### With CSS Variables
```css
.custom-component {
  background-color: var(--card);
  color: var(--card-foreground);
  border-radius: var(--radius);
  padding: var(--spacing-4);
}
```

## Implementation Notes

- Design system integrated into `/src/frontend/tailwind.config.js` and `/src/frontend/src/index.css`
- All colors, spacing, and typography defined as CSS variables for easy theming
- Tailwind config extends with custom design tokens
- Dark mode toggle via `class="dark"` on root element

## Dashboard Components

### Layout Structure

The dashboard follows a fixed sidebar + content area layout:

```
┌─────────────┬──────────────────────────────────────┐
│             │  Header (Live Indicator)             │
│   Sidebar   ├──────────────────────────────────────┤
│   (Fixed)   │  Metrics Cards (6-column grid)       │
│             ├──────────────────────────────────────┤
│  - Logo     │  Active Backup Jobs Table            │
│  - Nav      ├──────────────────────────────────────┤
│             │  Recently Completed Table            │
└─────────────┴──────────────────────────────────────┘
```

### Sidebar Component

**Location**: `/src/frontend/src/components/Sidebar.tsx`

**Purpose**: Fixed navigation sidebar with dark theme

**Specifications**:
- Width: 256px (64 * 4px base unit)
- Background: `--sidebar` (#1e293b)
- Fixed position, full height
- Logo area at top with border separator
- Navigation items with hover states
- Active route highlighting

**Navigation Items**:
- Dashboard (home)
- Devices (device management)
- Restore (file recovery)
- Settings (configuration)

**States**:
- Default: `text-sidebar-foreground`
- Hover: `bg-sidebar-accent/50`
- Active: `bg-sidebar-accent`

### Dashboard Header Component

**Location**: `/src/frontend/src/components/DashboardHeader.tsx`

**Purpose**: Page title with real-time status indicator

**Elements**:
1. **Title**: H1 with page name ("Live Monitoring Dashboard")
2. **Subtitle**: Optional description text
3. **Live Indicator**: 
   - Pulsing green dot (2px, animated)
   - "Live • Updated HH:MM:SS" text
   - Success green background badge
   - Updates every second

### Metric Cards

**Location**: `/src/frontend/src/components/MetricCard.tsx`

**Purpose**: Compact status display for key metrics

**Layout**: 6-column grid (responsive: 2 cols mobile, 3 cols tablet, 6 cols desktop)

**Card Anatomy**:
```
┌──────────────────┐
│ LABEL      [Icon]│  ← Small uppercase text + icon
│ 123              │  ← Large number (2xl)
│ [Optional badge] │  ← Status badge for errors
└──────────────────┘
```

**Variants**:
- `default`: Neutral gray icon
- `success`: Green icon/text for completed
- `warning`: Amber for attention needed
- `error`: Red with "Requires attention" badge

**Metrics Displayed**:
1. Active Jobs (activity icon)
2. Queued (clock icon)
3. Completed (check circle, green)
4. Failed (x circle, red if >0)
5. Avg Speed (gauge icon)
6. Data Today (database icon)

**Spacing**: 16px gap between cards

### Active Jobs Table

**Location**: `/src/frontend/src/components/ActiveJobsTable.tsx`

**Purpose**: Show running and pending backups with real-time progress

**Design Philosophy**: Maximum 2 lines per row to conserve vertical space

**Columns**:
1. **Device**: Icon + name (line 1), Current file path (line 2, muted, truncated)
2. **Status**: Animated badge (Running with spinner, Pending)
3. **Path**: Monospace font for share path
4. **Progress**: Bar + percentage (inline, ~200px bar)
5. **Speed**: Right-aligned, bold (e.g., "117 MB/s")
6. **ETA**: Right-aligned, muted (e.g., "4m 32s")

**Row Height**: ~60-80px (2-line content + padding)

**States**:
- Hover: `bg-muted/50` background
- Progress bar: Animated width transition (300ms)
- Running status: Spinning loader icon

**Empty State**: "No active backup jobs" centered message

### Recently Completed Table

**Location**: `/src/frontend/src/components/RecentlyCompletedTable.tsx`

**Purpose**: Show last 10 completed backups with action buttons

**Columns**:
1. **Device**: Icon + name
2. **Path**: Monospace share path
3. **Status**: Badge (Success green, Warning amber)
4. **Duration**: Time taken (e.g., "14m 32s")
5. **Data Transferred**: Size (e.g., "67.1 GB")
6. **Completed At**: Time (HH:MM:SS)
7. **Actions**: 2 buttons (View Details, Browse Files)

**Action Buttons**:
- **View Details**: Eye icon, navigate to device detail page
- **Browse Files**: Folder icon, navigate to file browser
- Style: Secondary button (`bg-secondary`, hover darker)
- Size: Small, inline-flex with icon

**Row Interaction**:
- Hover: Background highlight
- Clickable buttons only (row itself not clickable)

**Empty State**: "No completed backups yet" centered message

### Real-Time Updates

**Update Frequency**:
- Dashboard data: Every 5 seconds (API polling)
- Timestamp: Every 1 second (client-side)
- SignalR events: Immediate (when available)

**Data Flow**:
1. `dashboardService.getStats()` - Aggregate metrics
2. `dashboardService.getActiveJobs()` - Running/pending jobs
3. `dashboardService.getRecentBackups(10)` - Last 10 backups

**Error Handling**:
- Backend offline: Yellow warning banner with retry button
- API errors: Red error banner with details
- Graceful degradation: Show stale data with warning

### Responsive Behavior

**Breakpoints**:
- **Mobile (<768px)**: 
  - Metrics: 2 columns
  - Tables: Horizontal scroll
  - Sidebar: Collapsible (hamburger menu)
  
- **Tablet (768-1024px)**:
  - Metrics: 3 columns
  - Tables: Full width, may scroll
  
- **Desktop (>1024px)**:
  - Metrics: 6 columns
  - Tables: Full width, all columns visible

### Accessibility

- All interactive elements keyboard navigable
- ARIA labels on icon-only buttons
- Status colors also differentiated by icons (not color-blind reliant)
- Focus visible styles on all interactive elements
- Semantic HTML (table elements, proper headings)

## Next Steps

1. ✅ Dashboard component library implemented
2. ✅ Real-time data integration with backend API
3. Wire up SignalR for instant job updates (reduce polling)
4. Add dark mode toggle in UI
5. Create device management pages
6. Build file browser for restore functionality

## Resources

- Figma file: [Design System for Backup Tool](https://www.figma.com/design/D228AAj8oZMJTzwKBP3wG9/Design-System-for-Backup-Tool)
- Source design tokens: `/tmp/src/styles/theme.css` (exported from Figma)
- Implementation: `/src/frontend/tailwind.config.js`, `/src/frontend/src/index.css`
- Dashboard components: `/src/frontend/src/components/`
- Dashboard service: `/src/frontend/src/services/dashboardService.ts`
