---
name: review-and-commit
description: Automated workflow that runs comprehensive pre-commit review, automatically fixes any issues found, stages changes, and commits with a proper message if review passes. Use when user asks to "review and commit", "auto commit", "review then commit", or wants automated review + commit workflow.
---

# Review and Commit

Automated workflow that executes pre-commit review and handles the commit process intelligently based on review results.

## Workflow

### 1. Run Pre-Commit Review (MANDATORY - DO NOT SKIP)

**This step MUST be executed first before any other actions. Do not proceed to staging/committing without completing this step.**

Execute the comprehensive pre-commit-review skill to analyze all changes:

```
Invoke the pre-commit-review skill
```

This performs:
- Automated change analysis
- Test execution with coverage verification (>50% required)
- Specification alignment check
- Architecture review
- Security and maintainability assessment

**CRITICAL:** Block commit if review is not performed. Report findings before proceeding.

### 2. Process Review Results

Based on review findings, take appropriate action:

#### ❌ If Issues/Errors Found

**Automatically fix and retry:**

1. Analyze each issue and determine fix
2. Implement fixes using appropriate tools
3. Re-run pre-commit review
4. Repeat until no issues remain or maximum 3 iterations reached
5. If unable to resolve after 3 iterations, report to user with details

Common auto-fixes:
- Code formatting issues
- Missing dependencies
- Obvious bugs (null checks, type mismatches)
- Test failures with clear causes
- Security issues (exposed secrets, debug code)

#### ⚠️ If Warnings/Suggestions Found

**Ask user for guidance:**

Present warnings and suggestions clearly, then ask:
- "Should I address these warnings before committing?"
- "Do you want me to implement any of these suggestions?"

Wait for user response before proceeding.

#### ✅ If Review Passes (No Issues/Warnings/Suggestions)

**Automatically stage and commit:**

1. Stage all changes: `git add -A`
2. Generate proper commit message based on changes
3. Commit with generated message
4. Report commit hash and summary

### 3. Generate Commit Message

Create conventional commit message following this format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Type:** feat, fix, docs, style, refactor, test, chore, perf

**Scope:** Component/module affected (optional)

**Subject:** Concise summary (max 72 chars)

**Body:** Detailed explanation of what and why (optional, use for significant changes)

**Footer:** Breaking changes, issue references (optional)

#### Message Generation Strategy

Analyze changed files and determine:
1. Primary type based on change nature
2. Scope from affected modules/components
3. Concise subject summarizing the change
4. Body if changes are complex or need explanation
5. Footer for breaking changes or closed issues

**Examples:**

Simple change:
```
fix(restic): expand exception filter for repository initialization
```

Feature addition:
```
feat(dashboard): add dashboard summary endpoint

- Implement DashboardController with /api/dashboard/summary
- Add DTOs for dashboard data structure
- Integrate storage monitoring and job aggregation
- Calculate health status based on recent backup success
```

Multiple components:
```
refactor(tests): improve test infrastructure

- Register IResticClient interface in test factories
- Remove debug logging from ErrorHandlingMiddleware
- Add SchedulingFlowTests for Quartz integration
- Switch E2E factory to real service implementations
```

## Auto-Fix Guidelines

**Safe to auto-fix:**
- Formatting/linting errors
- Missing using statements
- Obvious null reference issues
- Test setup problems
- Debug code removal
- Exposed secrets/credentials

**Require user confirmation:**
- Logic changes that affect behavior
- Breaking changes to public APIs
- Database schema modifications
- Performance optimizations with tradeoffs
- Architectural refactoring

## Error Handling

**STRICT WORKFLOW ENFORCEMENT:**
- Review MUST be executed before staging/committing
- If review is not performed, the workflow is invalid and must restart
- Do not skip review steps regardless of perceived urgency
- Block commits until review is complete

**Maximum retry attempts for auto-fixes:** 3

After 3 failed fix attempts:
1. Report all issues that couldn't be auto-fixed
2. Provide analysis of why fixes failed
3. Suggest manual intervention steps
4. Do NOT commit

## Usage Examples

**Simple workflow:**
```
User: "review and commit"
→ Runs review
→ Passes with no issues
→ Commits: "fix(ui): handle future dates in formatDate"
```

**With auto-fix:**
```
User: "review and commit"
→ Runs review
→ Finds debug logging in middleware
→ Removes debug code automatically
→ Re-runs review
→ Passes
→ Commits: "fix(middleware): remove debug logging"
```

**With suggestions:**
```
User: "review and commit"
→ Runs review
→ Finds suggestions for refactoring
→ Asks: "Should I implement these suggestions before committing?"
→ User: "no, commit as is"
→ Commits current changes
```

## Integration with Pre-Commit Review

This skill leverages the pre-commit-review skill but adds automation:
- **Pre-commit-review:** Analysis and reporting only
- **Review-and-commit:** Analysis + auto-fix + commit

Both skills share the same quality standards and review criteria.
