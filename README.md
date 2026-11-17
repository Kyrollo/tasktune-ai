# TaskTune AI

TaskTune is an AI-assisted people and work operations platform. It brings employee and organization information together with projects, tasks, progress reviews, management reporting, and specialized AI agents.

[![License: MIT](https://img.shields.io/badge/License-MIT-emerald.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10%20LTS-512BD4.svg)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19-61DAFB.svg)](https://react.dev/)

## Product overview

The intended product direction is a modular People & Work Operating System with tenant-aware identity, employee records, organization structure, task and project workflows, HR operations, reporting, and controlled AI assistance.

The current repository contains:
- An ASP.NET Core Web API using the modern .NET 10 project set.
- SQL Server persistence through Entity Framework Core.
- A React 19 and Vite web frontend (JavaScript/JSX).
- Five Python agent areas built around FastAPI and LangGraph.
- Documentation, SQL setup scripts, and demonstration screenshots.

AI actions that affect people, approvals, or external communications must remain permission-checked and subject to human approval. A UI or agent implementation does not by itself mean that a workflow is production-ready.

## Demonstration

- [Launch the TaskTune demonstration](https://kyrollo.github.io/projects/tasktune)
- [Watch the 1080p demonstration video](https://kyrollo.github.io/media/tasktune/demo.mp4)
- [Browse the screenshot gallery](docs/assets/screenshots/)

Demonstration assets and previously reported performance figures are not contractual service levels or independently verified production measurements.

## Architecture

~~~mermaid
flowchart TD
    WEB["React 19 web client"] --> API["ASP.NET Core 10 API"]
    API --> DB["SQL Server / EF Core"]
    API --> AGENTS["Private Python agent services"]
    AGENTS --> LLM["Approved LLM providers"]
~~~

The modern backend projects are under tasktune-backend/TaskTuneAPI:
- TaskTune.API — HTTP API and application startup.
- TaskTune.Application — application contracts and DTOs.
- TaskTune.Infrastructure — EF Core data access and service implementations.
- TaskTune.Tests — authentication regression tests.

The older TaskTuneAPI.csproj project at the backend directory root targets .NET Framework 4.8 and is retained as a legacy project. It is not the API target used by the current CI pipeline.

## Repository layout

~~~text
tasktune-ai/
├── docs/                         # Product and engineering documentation
├── tasktune-agents/              # Python agent services
├── tasktune-backend/
│   ├── Script Database/          # SQL Server setup and seed scripts
│   └── TaskTuneAPI/
│       ├── TaskTune.API/         # ASP.NET Core API (.NET 10)
│       ├── TaskTune.Application/ # Application interfaces and DTOs
│       ├── TaskTune.Infrastructure/
│       └── TaskTune.Tests/       # Auth regression tests
├── tasktune-frontend/            # React / Vite application
├── global.json                  # .NET SDK selection
└── SETUP_GUIDE.md               # Local development instructions
~~~

## Local development

Follow [SETUP_GUIDE.md](SETUP_GUIDE.md) for SQL Server setup, local secrets, backend startup, Python agents, and frontend startup.

The API intentionally fails during startup when its database connection string or JWT signing key is missing. Configure those values through .NET User Secrets or environment variables; do not put credentials in tracked appsettings files.

## Quality checks

The GitHub Actions workflow validates the repository projects:
- .NET 10 API build.
- Authentication regression tests for BCrypt-only password verification and hashed/rotating refresh tokens.
- Frontend ESLint and production Vite build.
- Python source syntax compilation.

Run the equivalent checks locally:

~~~bash
dotnet build tasktune-backend/TaskTuneAPI/TaskTune.API/TaskTune.API.csproj --configuration Release
dotnet test tasktune-backend/TaskTuneAPI/TaskTune.Tests/TaskTune.Tests.csproj --configuration Release
cd tasktune-frontend
npm ci
npm run lint
npm run build
cd ..
python -m compileall -q tasktune-agents
~~~

## Security and release status

TaskTune is still under active engineering. Do not use customer production data until tenant isolation, authorization coverage, password recovery, deployment security, backups and restore drills, monitoring, and a formal security review have passed.

Self-service registration is temporarily disabled until tenant-scoped invitations and email verification are implemented. Use the documented development seed accounts only in a local, non-production environment.

## License

This repository currently includes the MIT License. Commercial distribution, ownership, contributor rights, and third-party dependency obligations require a separate legal review.
