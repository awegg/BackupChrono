# v0.dev Prompts for BackupChrono UI Components

## Prompt 1: Create New Device Form

Create a modern, responsive form for adding a new backup device with the following specifications:

**Form Title:** "Add New Device"

**Form Fields:**

1. **Device Name** (required)
   - Text input
   - Placeholder: "e.g., office-nas, web-server-1"
   - Validation: Alphanumeric, hyphens only (DNS-compatible), 1-100 characters
   - Helper text: "DNS-compatible name (alphanumeric and hyphens only)"

2. **Protocol** (required)
   - Dropdown/Select with options:
     - SMB/CIFS
     - SSH/SFTP
     - Rsync
   - Default: SMB/CIFS

3. **Host** (required)
   - Text input
   - Placeholder: "192.168.1.100 or nas.local"
   - Validation: Valid IP address or hostname, 1-255 characters
   - Helper text: "IP address or hostname"

4. **Port** (optional)
   - Number input
   - Placeholder: Auto-fills based on protocol (SMB=445, SSH=22, Rsync=873)
   - Validation: 1-65535
   - Helper text: "Leave empty for default port"

5. **Username** (required)
   - Text input
   - Placeholder: "backup-user"
   - Validation: 1-100 characters

6. **Password** (required)
   - Password input with show/hide toggle
   - Placeholder: "ÔÇóÔÇóÔÇóÔÇóÔÇóÔÇóÔÇóÔÇó"
   - Helper text: "Stored encrypted"

**Advanced Settings Section** (collapsible/expandable):

7. **Wake-on-LAN**
   - Toggle switch (default: off)
   - When enabled, show:
     - **MAC Address** field (required if WOL enabled)
     - Format: XX:XX:XX:XX:XX:XX
     - Validation: Valid MAC address format

8. **Device-Level Schedule** (optional)
   - Cron expression input or visual cron builder
   - Placeholder: "0 2 * * *" (2 AM daily)
   - Helper text: "Leave empty to use global default schedule"
   - Include a "Schedule Builder" button that opens a helper modal

9. **Retention Policy** (optional)
   - Collapsible section with fields:
     - Latest backups to keep (number input, placeholder: 7)
     - Daily backups to keep (number input, placeholder: 7)
     - Weekly backups to keep (number input, placeholder: 4)
     - Monthly backups to keep (number input, placeholder: 12)
     - Yearly backups to keep (number input, placeholder: 3)
   - Helper text: "Leave empty to use global defaults"

10. **Include/Exclude Patterns** (optional)
    - Two text areas with monospace font:
      - **Include Patterns** (glob patterns, one per line)
        - Placeholder: "*.pdf\n*.docx\nDocuments/**"
      - **Exclude Patterns** (glob or regex, one per line)
        - Placeholder: "*.tmp\n.git/**\nnode_modules/**"
    - Helper text: "Leave empty to use global defaults. One pattern per line."

**Form Actions:**
- "Cancel" button (secondary, left-aligned)
- "Test Connection" button (secondary, shows connection status)
- "Add Device" button (primary, right-aligned, disabled until required fields are valid)

**Design Requirements:**
- Clean, modern interface with shadcn/ui or similar component library
- Responsive layout (works on desktop and tablet)
- Show validation errors inline below each field
- Use icons for protocol selection
- Expandable/collapsible sections for advanced settings
- Loading state for "Test Connection" button
- Success/error toast notifications after form submission
- Form should have a max-width of 800px and be centered

**Color Scheme:**
- Use a professional, modern color palette
- Primary action: Blue or teal
- Error states: Red
- Success states: Green
- Neutral backgrounds and borders

---

## Prompt 2: Share Configuration Form

Create a modern, responsive form for adding a share to an existing device with the following specifications:

**Form Title:** "Add Share to [Device Name]"
**Subtitle:** Show device details: "Device: office-nas (192.168.1.100, SMB)"

**Form Fields:**

1. **Share Name** (required)
   - Text input
   - Placeholder: "e.g., documents, photos, var-www"
   - Validation: Unique within device, 1-100 characters
   - Helper text: "Descriptive name for this share"

2. **Share Path** (required)
   - Text input
   - Placeholder: Auto-adjust based on device protocol:
     - SMB: "\\share\\folder" or "share/folder"
     - SSH/Rsync: "/data/backups" or "/var/www"
   - Validation: 1-500 characters, format validated based on protocol
   - Helper text: "Path on the device to back up"

3. **Enabled** (default: true)
   - Toggle switch
   - Label: "Enable backups for this share"
   - Helper text: "Disable to temporarily pause backups without deleting configuration"

**Configuration Overrides Section** (collapsible/expandable):

Display a clear visual indicator showing the hierarchy:
```
Global Defaults ÔåÆ Device Settings ÔåÆ Share Settings (you are here)
```

4. **Schedule Override** (optional)
   - Checkbox: "Override device/global schedule"
   - When checked, show:
     - Cron expression input or visual cron builder
     - Placeholder: "0 2 * * *"
     - "Schedule Builder" button
   - Show inherited schedule from device/global:
     - Example: "Currently using: Device schedule (2 AM daily)" or "Global schedule (2 AM daily)"

5. **Retention Policy Override** (optional)
   - Checkbox: "Override device/global retention policy"
   - When checked, show fields:
     - Latest backups to keep
     - Daily backups to keep
     - Weekly backups to keep
     - Monthly backups to keep
     - Yearly backups to keep
   - Show inherited values:
     - Example: "Currently using: Device policy (7/7/4/12/3)" or "Global policy (7/7/4/12/3)"

6. **Include/Exclude Patterns Override** (optional)
   - Checkbox: "Override device/global patterns"
   - When checked, show two text areas:
     - **Include Patterns** (one per line)
     - **Exclude Patterns** (one per line)
   - Show inherited patterns in a read-only preview above:
     - "Inherited patterns from device:" with expandable list

**Configuration Preview Panel:**
- Fixed sidebar or bottom panel showing "Effective Configuration"
- Displays:
  - Schedule: "[Source: Global/Device/Share] Cron: 0 2 * * *"
  - Retention: "[Source: Global/Device/Share] 7/7/4/12/3"
  - Patterns: "[Source: Global/Device/Share] X includes, Y excludes"

**Form Actions:**
- "Cancel" button (secondary, left-aligned)
- "Add Share" button (primary, right-aligned, disabled until required fields are valid)

**Design Requirements:**
- Clean, modern interface with shadcn/ui or similar component library
- Responsive layout
- Clear visual hierarchy showing configuration cascade
- Show inherited values prominently with a different visual style (e.g., lighter text, dotted border)
- Use badges or chips to indicate "Inherited", "Override", or "Default" for each setting
- Icons to indicate override status
- Expandable/collapsible sections
- Inline validation with helpful error messages
- Success toast notification after adding share
- Form max-width: 900px (wider than device form to accommodate preview panel)

**Visual Enhancements:**
- Use a tree or flow diagram icon to show configuration cascade
- Color-code sources: Global (gray), Device (blue), Share (green)
- Show toggle animations when enabling overrides
- Add tooltips explaining inheritance behavior

**Color Scheme:**
- Use a professional, modern color palette
- Primary action: Blue or teal
- Override indicators: Amber/orange
- Inherited values: Gray/muted
- Success states: Green
- Neutral backgrounds and borders

---

## Additional Context

**System Overview:**
BackupChrono is a modern central-pull backup system with a device ÔåÆ share hierarchy. Configuration cascades from Global Defaults ÔåÆ Device-Level ÔåÆ Share-Level, with more specific settings taking precedence.

**Key User Flows:**
1. Admin adds a device with connection credentials
2. Admin adds one or more shares under that device
3. Each share can optionally override device or global settings
4. System automatically backs up shares according to schedules

**Design Philosophy:**
- Make the common case simple (minimal required fields)
- Make advanced configuration accessible but not overwhelming (collapsible sections)
- Show inheritance clearly so users understand effective configuration
- Provide helpful defaults and validation
- Modern, professional aesthetic suitable for system administrators

**Tech Stack Reference:**
- Frontend: React + TypeScript + Vite
- Styling: Tailwind CSS
- Component library: shadcn/ui preferred
- Form validation: Consider react-hook-form + zod

---

## Usage Instructions

1. Copy "Prompt 1" and paste into v0.dev to generate the "Add Device" form
2. Copy "Prompt 2" and paste into v0.dev to generate the "Add Share" form
3. Review and iterate on the generated components
4. Export the React/TypeScript code and integrate into the BackupChrono frontend

These prompts are based on the BackupChrono specification (specs/001-backup-system/spec.md and data-model.md).
