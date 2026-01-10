# Pre-Commit Review Checklist

This checklist provides a structured approach for reviewing code changes before commit. Use this as a guide to ensure comprehensive code review covering all critical aspects.

## 1. Specification Alignment

**Verify changes align with documented requirements:**

- [ ] Changes are referenced in spec.md or related user stories
- [ ] Implementation matches the specified behavior
- [ ] No undocumented feature additions or changes
- [ ] Any deviations from spec are justified and documented

**Key questions:**
- What spec/user story does this change address?
- Does the implementation fully satisfy the requirements?
- Are there any edge cases not covered by the spec?

**Where to look:**
- `/specs/001-backup-system/spec.md` - Main feature specification
- `/specs/001-backup-system/tasks.md` - Task breakdown
- `/specs/001-backup-system/checklists/requirements.md` - Requirements checklist

## 2. Consistency & Unnecessary Changes

**Ensure focused, consistent changes:**

- [ ] Changes are focused on the stated purpose
- [ ] No unrelated modifications or refactoring
- [ ] Formatting is consistent with existing codebase
- [ ] Naming conventions match project standards
- [ ] No commented-out code blocks
- [ ] No debug statements or console logs

**Red flags:**
- Large files with minimal actual changes (whitespace only)
- Mixing feature work with unrelated cleanup
- Inconsistent code style within the same file
- Multiple responsibilities in a single commit

## 3. Test Coverage

**Minimum 50% code coverage required:**

- [ ] Tests exist for new functionality
- [ ] Tests cover happy path scenarios
- [ ] Tests cover error/edge cases
- [ ] Existing tests still pass
- [ ] Code coverage >= 50%
- [ ] Integration tests if applicable

**Test quality criteria:**
- Tests are meaningful, not just for coverage metrics
- Test names clearly describe what is being tested
- Tests are independent and can run in any order
- Mocks/stubs are used appropriately

**Coverage analysis:**
Run `scripts/run_tests_with_coverage.ps1` to verify coverage threshold.

## 4. Architecture

**Verify architectural soundness:**

- [ ] Follows project layer structure (Core, Infrastructure, Api)
- [ ] Dependencies flow in correct direction (no circular references)
- [ ] Proper separation of concerns
- [ ] Interfaces used appropriately for abstraction
- [ ] No leaky abstractions
- [ ] Domain entities in Core, implementation in Infrastructure

**Architecture patterns used in this project:**
- **Clean Architecture**: Core (domain), Infrastructure (implementation), Api (presentation)
- **Repository Pattern**: Data access abstraction
- **Dependency Injection**: Service registration and resolution
- **CQRS considerations**: Command/Query separation where appropriate

**Key questions:**
- Does this belong in Core, Infrastructure, or Api?
- Are we introducing new dependencies that violate layer boundaries?
- Is the abstraction at the right level?

## 5. Maintainability

**Code should be easy to understand and modify:**

- [ ] Code is self-documenting with clear names
- [ ] Complex logic has explanatory comments
- [ ] Functions/methods are single-purpose and reasonably sized
- [ ] No code duplication (DRY principle)
- [ ] Error messages are helpful and actionable
- [ ] Logging is appropriate (not too verbose, not too sparse)
- [ ] Configuration is externalized where appropriate

**Maintainability metrics:**
- Methods > 50 lines should be rare and justified
- Cyclomatic complexity kept reasonable
- Class responsibilities are clear and focused
- Changes don't make the codebase harder to understand

**Code smells to watch for:**
- Long parameter lists (> 4-5 parameters)
- Deep nesting (> 3 levels)
- Magic numbers/strings
- Primitive obsession
- Feature envy (method using more of another class than its own)

## 6. Security & Safety

**Basic security checks:**

- [ ] No sensitive data in code (passwords, keys, tokens)
- [ ] No secrets in configuration files being committed
- [ ] Input validation for user-provided data
- [ ] Proper error handling (no sensitive info in error messages)
- [ ] SQL injection prevention (parameterized queries)
- [ ] Path traversal prevention for file operations

**Files to never commit:**
- `.env` files with secrets
- `appsettings.Production.json` with real credentials
- Private keys (`.key`, `.pem`)
- API tokens or passwords
- Build output directories (`bin/`, `obj/`, `node_modules/`)
- Test results (`TestResults/`, `*.trx`)
- Diagnostic/debug files (`diag.txt`, `output.txt`, `results.txt`, `test_output.txt`)
- IDE caches (`.vs/`, `__pycache__/`)
- Packaged skill files (`*.skill`)
- Log files (`*.log`)
- Temporary files (`temp*`, `tmp*`)

## 7. Performance Considerations

**Basic performance review:**

- [ ] No obvious performance bottlenecks (N+1 queries, unnecessary loops)
- [ ] Async/await used appropriately for I/O operations
- [ ] Resources properly disposed (using statements)
- [ ] Large collections processed efficiently
- [ ] Caching considered where appropriate

## Review Workflow

1. **Run automated checks**: Execute analysis scripts
2. **Review each section**: Work through checklist systematically
3. **Document findings**: Highlight issues clearly at the beginning
4. **Suggest fixes**: Provide actionable recommendations
5. **Verify tests**: Ensure coverage meets threshold
6. **Final decision**: Approve or request changes

## Output Format

When conducting a review, structure findings as:

**Issues (must fix):**
- Critical problems that block commit

**Warnings (should address):**
- Concerns that should be considered but may not block

**Suggestions (optional improvements):**
- Nice-to-have improvements for future consideration
