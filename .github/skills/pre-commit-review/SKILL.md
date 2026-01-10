---
name: pre-commit-review
description: Comprehensive code review before committing changes. Verifies alignment with specifications, ensures consistency, runs tests with 50%+ coverage requirement, and evaluates architecture and maintainability. Use when the user asks to "review my code", "review before commit", "check my changes", or before committing to ensure code quality.
---

# Pre-Commit Review

Systematic code review workflow to ensure changes are specification-aligned, well-tested, architecturally sound, and maintainable before committing.

## Review Process

Follow this workflow for comprehensive pre-commit reviews:

### 1. Get Changed Files

Run automated analysis to understand the scope of changes:

```powershell
.github/skills/pre-commit-review/scripts/analyze_changes.ps1
```

This highlights:
- Files changed and line counts
- Large changes (>500 lines)
- Potential issues (debug code, sensitive files)
- Suspicious patterns

### 2. Conduct Systematic Review

Use the structured checklist in [references/review-checklist.md](references/review-checklist.md) to review:

1. **Specification Alignment** - Verify changes match spec.md requirements
2. **Consistency & Unnecessary Changes** - Ensure focused, clean changes
3. **Test Coverage** - Verify >50% coverage requirement
4. **Architecture** - Check layer boundaries and design patterns
5. **Maintainability** - Assess code clarity and long-term viability
6. **Security** - Check for secrets, vulnerabilities
7. **Performance** - Look for obvious bottlenecks

### 3. Run Tests with Coverage

Execute the test suite and verify coverage threshold:

```powershell
.github/skills/pre-commit-review/scripts/run_tests_with_coverage.ps1
```

**Coverage requirement: >50%**

If coverage is below threshold, identify untested code and request additional tests.

Then, build Docker images to validate production build integrity:

```powershell
# Backend and frontend container builds (must succeed)
docker build -f docker/Dockerfile.backend -t backupchrono-backend:review .
docker build -f docker/Dockerfile.frontend -t backupchrono-frontend:review .
```

If Docker is not available or a build fails, treat as a blocking issue and stop the review.

### 4. Check Specification Alignment

Read relevant spec files to verify implementation matches requirements:
- `/specs/001-backup-system/spec.md` - Feature specifications
- `/specs/001-backup-system/tasks.md` - Task breakdown
- `/specs/001-backup-system/checklists/requirements.md` - Requirements checklist

Verify:
- Changes are traceable to documented requirements
- Implementation behavior matches spec
- No undocumented features or deviations

### 5. Architectural Review

Verify adherence to Clean Architecture:
- **Core** (Domain): Entities, interfaces, value objects - no infrastructure dependencies
- **Infrastructure**: Implementations, external services, data access
- **Api**: Controllers, DTOs, presentation layer

Check:
- Dependencies flow inward (Api → Infrastructure → Core)
- No circular references
- Proper abstraction through interfaces
- Repository pattern usage for data access

### 6. Report Findings

Structure review output clearly:

**❌ Issues (must fix before commit):**
- Critical problems that block commit
- Security vulnerabilities
- Test failures or insufficient coverage
- Specification violations

**⚠️ Warnings (should address):**
- Code smells or maintainability concerns
- Architectural deviations
- Missing tests for edge cases
- Consistency problems

**💡 Suggestions (optional improvements):**
- Refactoring opportunities
- Performance optimizations
- Code clarity enhancements

## Key Project Patterns

**Architecture:** Clean Architecture with Core, Infrastructure, Api layers

**Testing:** 
- Unit tests in BackupChrono.UnitTests
- Integration tests in BackupChrono.IntegrationTests
- Restic-specific tests in BackupChrono.Infrastructure.Restic.Tests

**Naming Conventions:**
- PascalCase for classes, methods, properties
- camelCase for local variables, parameters
- Interfaces prefixed with `I`
- Async methods suffixed with `Async`

**Dependency Injection:** Services registered in Program.cs, injected via constructor

## Example Review Flow

1. User stages changes with `git add`
2. User requests: "Review my code before commit"
3. Run `analyze_changes.ps1` to get overview
4. Run `run_tests_with_coverage.ps1` to verify tests
5. Read changed files to understand implementation
6. Check relevant specs for alignment
7. Apply checklist systematically
8. Report issues at the beginning, warnings next, suggestions last
9. Provide clear approval or change requests

## Resources

- `scripts/analyze_changes.ps1` - Automated change analysis
- `scripts/run_tests_with_coverage.ps1` - Test execution with coverage
- `references/review-checklist.md` - Comprehensive review checklist
