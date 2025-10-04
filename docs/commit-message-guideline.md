# Git Commit Guidelines - Pipster Project

Welcome to the Pipster development team! This document outlines our Git commit conventions to maintain a clean, readable, and professional commit history.

---

## Table of Contents
1. [Why Commit Conventions Matter](#why-commit-conventions-matter)
2. [Commit Message Format](#commit-message-format)
3. [Commit Types](#commit-types)
4. [Writing Good Commit Messages](#writing-good-commit-messages)
5. [Examples](#examples)
6. [Common Mistakes to Avoid](#common-mistakes-to-avoid)
7. [Tools and Automation](#tools-and-automation)

---

## Why Commit Conventions Matter

Good commit messages help us:
- **Understand changes** without reading the code
- **Generate changelogs** automatically
- **Track features and fixes** across versions
- **Collaborate effectively** with teammates
- **Debug issues** by understanding when/why changes were made
- **Automate versioning** using semantic versioning

---

## Commit Message Format

We follow the **Conventional Commits** specification:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Structure Breakdown

#### 1. **Type** (Required)
The category of change. See [Commit Types](#commit-types) below.

#### 2. **Scope** (Optional)
The area of the codebase affected:
- `api` - API endpoints
- `worker` - Background workers
- `domain` - Domain entities/logic
- `infra` - Infrastructure layer
- `telegram` - Telegram integration
- `auth` - Authentication
- `billing` - Billing/payments

**Examples:**
```
feat(api): add tenant creation endpoint
fix(telegram): resolve message duplication issue
```

#### 3. **Subject** (Required)
- Brief description (50 characters or less)
- Use imperative mood: "add" not "added" or "adds"
- No period at the end
- Lowercase after the colon

#### 4. **Body** (Optional but Recommended)
- Detailed explanation of **what** and **why** (not how)
- Wrap at 72 characters per line
- Separate from subject with blank line
- Use bullet points for multiple changes

#### 5. **Footer** (Optional)
- Breaking changes: `BREAKING CHANGE: description`
- Issue references: `Closes #123`, `Fixes #456`
- Co-authors: `Co-authored-by: Name <email>`

---

## Commit Types

### Primary Types (Use These Most Often)

#### `feat:` - New Feature ✨
**When**: Adding new functionality that users or developers can use.

**Semantic Versioning Impact**: Minor version bump (1.0.0 → 1.1.0)

**Examples:**
```bash
feat: implement tenant registry with clean architecture
feat(api): add channel management endpoints
feat(telegram): add multi-channel monitoring support
feat(billing): integrate Stripe payment processing
```

---

#### `fix:` - Bug Fix 🐛
**When**: Fixing a bug, error, or incorrect behavior.

**Semantic Versioning Impact**: Patch version bump (1.0.0 → 1.0.1)

**Examples:**
```bash
fix: resolve null reference in signal parser
fix(telegram): prevent duplicate message processing
fix(api): correct validation error in tenant creation
fix(worker): handle connection timeout gracefully
```

---

#### `chore:` - Maintenance 🔧
**When**: Changes to build process, dependencies, configs, or dev tools that don't affect runtime behavior.

**Semantic Versioning Impact**: No version bump

**Examples:**
```bash
chore: update NuGet packages to latest versions
chore: configure Swagger to launch by default
chore(ci): add GitHub Actions workflow
chore: add .editorconfig for code formatting
```

---

### Supporting Types

#### `docs:` - Documentation 📚
**When**: Documentation-only changes (README, XML comments, etc.)

**Examples:**
```bash
docs: add API endpoint examples to README
docs: update architecture diagram
docs(api): add XML comments to TenantsController
```

---

#### `refactor:` - Code Refactoring ♻️
**When**: Code restructuring without changing external behavior.

**Examples:**
```bash
refactor: extract parsing logic into separate service
refactor(domain): simplify TradingConfiguration validation
refactor: move repository interfaces to Domain layer
```

---

#### `test:` - Tests ✅
**When**: Adding or updating tests (unit, integration, E2E).

**Examples:**
```bash
test: add unit tests for Tenant entity
test(integration): add end-to-end message processing tests
test: increase code coverage for TenantService
```

---

#### `perf:` - Performance Improvements ⚡
**When**: Changes that improve performance without changing functionality.

**Examples:**
```bash
perf: add caching to tenant config provider
perf(telegram): reduce memory allocation in message handler
perf: optimize database queries with indexes
```

---

#### `style:` - Code Style 💄
**When**: Formatting changes (whitespace, semicolons, etc.) with no code logic changes.

**Examples:**
```bash
style: format code with dotnet format
style: fix indentation in Program.cs
style: apply consistent naming conventions
```

---

#### `ci:` - Continuous Integration 🤖
**When**: Changes to CI/CD pipeline configuration.

**Examples:**
```bash
ci: add automated testing to GitHub Actions
ci: configure deployment to Azure App Service
ci: add code coverage reporting
```

---

#### `build:` - Build System 🏗️
**When**: Changes to build configuration (.csproj, build scripts, etc.)

**Examples:**
```bash
build: update target framework to net9.0
build: add Docker support
build: configure multi-stage Docker build
```

---

#### `revert:` - Revert Previous Commit ⏪
**When**: Reverting a previous commit.

**Format:**
```bash
revert: feat(api): add tenant creation endpoint

This reverts commit abc123def456.
Reason: Breaking change in production.
```

---

## Writing Good Commit Messages

### DO ✅

1. **Use imperative mood** in the subject line
   - ✅ "Add tenant repository"
   - ❌ "Added tenant repository"
   - ❌ "Adds tenant repository"

2. **Be specific and descriptive**
   - ✅ "fix: prevent null reference in signal parser when entry price is missing"
   - ❌ "fix: bug fix"

3. **Explain the WHY in the body**
   ```
   feat: add cached tenant config provider

   Tenant configurations are read on every message (high frequency).
   Added 5-minute TTL cache to reduce database load and improve
   message processing latency from ~200ms to ~50ms.
   ```

4. **Reference issues when applicable**
   ```
   fix: resolve memory leak in Telegram client

   Closes #42
   ```

5. **Indicate breaking changes clearly**
   ```
   feat!: change tenant ID format from int to GUID

   BREAKING CHANGE: Tenant IDs are now GUIDs instead of integers.
   Existing databases must run migration script before upgrading.
   ```

### DON'T ❌

1. **Don't use vague messages**
   - ❌ "fix stuff"
   - ❌ "updates"
   - ❌ "WIP"
   - ❌ "changes"

2. **Don't commit unrelated changes together**
   - ❌ One commit with both "add authentication" AND "fix parsing bug"
   - ✅ Two separate commits

3. **Don't include sensitive information**
   - ❌ API keys, passwords, credentials in commit messages

4. **Don't write novels**
   - Keep subject line under 50 characters
   - Keep body lines under 72 characters

---

## Examples

### Simple Feature

```bash
feat: add whitelist/blacklist symbol management

Added methods to TradingConfigService for managing symbol
whitelists and blacklists. Symbols are validated and stored
in uppercase for consistency.
```

### Bug Fix with Context

```bash
fix(telegram): prevent duplicate message processing

Messages were being processed multiple times due to race
condition in idempotency check. Added atomic Redis SET NX
operation to ensure exactly-once processing.

Fixes #123
```

### Breaking Change

```bash
feat!: migrate to new tenant configuration schema

BREAKING CHANGE: TradingConfiguration now uses separate
WhitelistedSymbols and BlacklistedSymbols properties instead
of a single AllowedSymbols list.

Migration guide:
1. Run database migration: dotnet ef database update
2. Update API client code to use new properties

Closes #45
```

### Chore with Multiple Changes

```bash
chore: update project dependencies

- Updated Microsoft.Extensions.Hosting to 9.0.0
- Updated Swashbuckle.AspNetCore to 9.0.4
- Updated StackExchange.Redis to 2.9.25
- All tests passing after updates
```

### Refactoring

```bash
refactor(domain): extract validation logic from entities

Moved validation logic from Tenant, ChannelConfiguration, and
TradingConfiguration into separate validator classes following
the Specification pattern. This improves testability and allows
validation rules to be composed and reused.
```

---

## Common Mistakes to Avoid

### 1. Wrong Commit Type

❌ **Wrong:**
```bash
feat: update README with installation instructions
```

✅ **Correct:**
```bash
docs: add installation instructions to README
```

---

### 2. Too Broad Scope

❌ **Wrong:**
```bash
feat: add stuff
```

✅ **Correct:**
```bash
feat(api): add tenant creation endpoint
feat(domain): add Tenant entity with validation
```

---

### 3. Mixing Unrelated Changes

❌ **Wrong:**
```bash
feat: add authentication and fix parsing bug
```

✅ **Correct:**
```bash
# Commit 1
feat(auth): add JWT authentication

# Commit 2
fix(parser): handle missing entry price gracefully
```

---

### 4. Non-Imperative Mood

❌ **Wrong:**
```bash
feat: added tenant repository
feat: adds channel management
```

✅ **Correct:**
```bash
feat: add tenant repository
feat: add channel management
```

---

### 5. Forgetting Breaking Changes

❌ **Wrong:**
```bash
feat: change API response format
```

✅ **Correct:**
```bash
feat!: change API response format to JSON:API spec

BREAKING CHANGE: All API responses now follow JSON:API
specification. Clients must update to parse new format.
```

---

## Tools and Automation

### 1. Git Commit Template

Create a commit message template:

**`.gitmessage` (in project root):**
```
# <type>(<scope>): <subject>
#
# <body>
#
# <footer>

# Type: feat, fix, docs, style, refactor, perf, test, chore, ci, build, revert
# Scope: api, worker, domain, infra, telegram, auth, billing
# Subject: imperative mood, lowercase, no period, max 50 chars
# Body: what and why (not how), wrap at 72 chars
# Footer: BREAKING CHANGE, Closes #issue
```

**Configure Git to use it:**
```bash
git config commit.template .gitmessage
```

---

### 2. Commitlint (Optional)

Install commitlint to enforce conventions:

```bash
npm install --save-dev @commitlint/cli @commitlint/config-conventional
```

**commitlint.config.js:**
```javascript
module.exports = {
  extends: ['@commitlint/config-conventional']
};
```

---

### 3. Husky (Optional)

Prevent bad commits with Git hooks:

```bash
npm install --save-dev husky
npx husky install
npx husky add .husky/commit-msg 'npx --no -- commitlint --edit "$1"'
```

---

### 4. Conventional Changelog

Auto-generate changelogs from commits:

```bash
npm install -g conventional-changelog-cli
conventional-changelog -p angular -i CHANGELOG.md -s
```

---

## Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│ Commit Type Decision Tree                               │
├─────────────────────────────────────────────────────────┤
│                                                         │
│ Does it add new functionality?                          │
│ ├─ Yes → feat:                                          │
│ └─ No                                                   │
│    ├─ Does it fix a bug? → fix:                        │
│    └─ No                                                │
│       ├─ Config/tooling/deps? → chore:                 │
│       ├─ Documentation only? → docs:                    │
│       ├─ Code restructuring? → refactor:               │
│       ├─ Performance improvement? → perf:              │
│       ├─ Tests only? → test:                           │
│       ├─ Formatting only? → style:                     │
│       ├─ CI/CD changes? → ci:                          │
│       └─ Build system? → build:                        │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## Summary

- **Use conventional commits** for consistency
- **Be descriptive** in both subject and body
- **Use imperative mood** ("add" not "added")
- **Reference issues** when applicable
- **Indicate breaking changes** explicitly
- **Commit often** with focused, atomic changes
- **Think about future you** reading the commit log

---

## Additional Resources

- [Conventional Commits Specification](https://www.conventionalcommits.org/)
- [Semantic Versioning](https://semver.org/)
- [How to Write a Git Commit Message](https://chris.beams.io/posts/git-commit/)
- [Angular Commit Message Guidelines](https://github.com/angular/angular/blob/main/CONTRIBUTING.md#commit)

---

**Questions?** Ask the team lead or open a discussion in our project chat!

**Happy committing! 🚀**