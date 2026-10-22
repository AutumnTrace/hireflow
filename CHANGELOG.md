# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Configurable CORS (`Cors:AllowedOrigins`) so browser front ends on other origins can call the API

### Fixed

- `JobApplication.moveTo` was unintentionally exposed as a query field by Hot Chocolate's method inference; stage changes now only go through `moveApplication`

## [1.0.0] - 2026-10-22

First stable release.

### Added

- **GraphQL API** for companies, job postings, candidates, applications and interviews (Hot Chocolate 16 on .NET 10)
- Cursor pagination, filtering and sorting on every list field
- Batched, source-generated **DataLoaders** for every relationship (no N+1 queries)
- **Hiring pipeline state machine** (`Applied → Screening → Interview → Offer → Hired`, plus `Rejected` / `Withdrawn`) with a full stage history and `allowedNextStages`
- Per-job funnel analytics: `pipeline { total hireRate byStage }`
- **Skill matching:** `skillMatch` on applications and ranked `recommendedJobs` on candidates (#1)
- `interviews` and `upcomingInterviews(days, interviewer)` queries (#2)
- **Typed domain errors** via mutation conventions (`InvalidStageTransitionError`, `DuplicateApplicationError`, …)
- **Subscriptions** over WebSockets: `onApplicationStageChanged`, `onApplicationSubmitted`
- JWT bearer authentication with `Recruiter` / `Admin` roles, applied to HTTP *and* WebSocket sessions
- `/health/live` and `/health/ready` probes with a database check (#2)
- SQLite (default) and PostgreSQL support, plus demo seed data
- Dockerfile, `docker compose` stack, and a GitHub Actions CI matrix covering both databases
- Docker image published to GitHub Container Registry on every release tag
- Dependabot, issue and pull request templates (#3)

[Unreleased]: https://github.com/AutumnTrace/hireflow/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/AutumnTrace/hireflow/releases/tag/v1.0.0
