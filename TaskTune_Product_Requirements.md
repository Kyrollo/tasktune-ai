# TaskTune AI — Product Requirements Document (PRD)

**Version:** 1.0  
**Date:** March 8, 2026  
**Project:** TaskTune AI — Integrated Work & HR Management System  
**Document Type:** Product Requirements & Implementation Roadmap  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Project Overview & Vision](#2-project-overview--vision)
3. [Current State Assessment](#3-current-state-assessment)
4. [Implementation Parts Breakdown](#4-implementation-parts-breakdown)
   - Part A: Frontend (React SPA)
   - Part B: Backend (C# .NET Web API)
   - Part C: Database (SQL Server)
   - Part D: AI Agents Integration
   - Part E: File Storage & Infrastructure
   - Part F: DevOps & Deployment
   - Part G: Testing & QA
   - Part H: Promotional Video (Remotion)
5. [Detailed Backend Specification](#5-detailed-backend-specification)
6. [Detailed Frontend Specification](#6-detailed-frontend-specification)
7. [Detailed Database Specification](#7-detailed-database-specification)
8. [Detailed AI Agents Specification](#8-detailed-ai-agents-specification)
9. [Detailed Infrastructure Specification](#9-detailed-infrastructure-specification)
10. [Detailed DevOps Specification](#10-detailed-devops-specification)
11. [Detailed Testing Specification](#11-detailed-testing-specification)
12. [Detailed Video Specification](#12-detailed-video-specification)
13. [General Recommendations](#13-general-recommendations)
14. [Priority & Phasing Plan](#14-priority--phasing-plan)

---

## 1. Executive Summary

TaskTune AI is a comprehensive Work & HR Management System combining project management, task tracking, performance evaluation, and AI-powered insights. The platform supports 6 user roles (Employee, Supervisor, Manager, CEO, HR, Admin), 20+ database tables, 24 system pages, and 5 specialized AI agents.

**Current State:** The frontend is a fully built, polished React UI prototype (34 source files, all functional components) running entirely on mock data. No backend, no database, no API integration, no authentication, and no data persistence exist yet.

**What This Document Covers:** Complete requirements for every remaining part of the system — backend API, database schema, AI integration, infrastructure, testing, DevOps — with detailed specifications, technology recommendations, and actionable task breakdowns.

---

## 2. Project Overview & Vision

### 2.1 Problem Statement

Organizations struggle with fragmented tools for task management, performance evaluation, and HR operations. This leads to:
- Disconnected workflows between managers, supervisors, and employees
- Manual and inconsistent performance evaluations
- Lack of real-time visibility into organizational productivity
- No AI-driven insights for decision-making

### 2.2 Solution

TaskTune AI provides a unified platform that:
- Centralizes project/task management with role-based access
- Automates performance evaluation with AI-powered feedback
- Provides real-time dashboards for every organizational level
- Integrates 5 specialized AI agents for intelligent guidance and automation
- Supports multi-tenant architecture (multiple companies)

### 2.3 Target Users & Roles

| Role | Primary Use | Dashboard |
|---|---|---|
| **Employee** | View assigned tasks, submit progress, view evaluations | Task-centric dashboard with AI guidance |
| **Supervisor** | Monitor team, review submissions, evaluate performance | Team oversight with alerts |
| **Manager** | Oversee department projects, generate reports | Department analytics & project health |
| **CEO** | Company-wide strategic view, role matrix control | Executive KPIs & AI summaries |
| **HR** | Monitor engagement, send communications, flag issues | Employee wellness & warning flags |
| **Admin** | System configuration, user management, AI settings | System health & audit logs |

---

## 3. Current State Assessment

### 3.1 What Is COMPLETED ✅

#### Frontend UI (React SPA) — ~85% Complete (UI Only)

| Component | Status | Details |
|---|---|---|
| **Project Setup** | ✅ Done | Vite + React 19 + Tailwind CSS v4 + Recharts + Lucide Icons |
| **Routing System** | ✅ Done | 20+ routes with `react-router-dom` v7, `ProtectedRoute` wrapper |
| **Theme System** | ✅ Done | Dark/Light/System with localStorage persistence, CSS custom properties |
| **Demo Auth Context** | ✅ Done | Role-based login (6 roles), navigation config per role |
| **Sidebar Navigation** | ✅ Done | Collapsible, role-based nav items, user info, logout |
| **Top Header** | ✅ Done | Page title, notification bell, user display |
| **UI Component Library** | ✅ Done | 11 components: Card, StatCard, Badge, Button, Input, Select, DataTable, ProgressBar, Modal, Tabs, EmptyState |
| **Mock Data System** | ✅ Done | 10 users, 5 projects, 8 tasks, evaluations, notifications, audit logs, chart datasets, AI config |
| **Landing Page** | ✅ Done | Hero, stats, features, roles showcase, CTA, footer |
| **Login Page** | ✅ Done | Role selector, email/password fields, demo mode |
| **SignUp Page** | ✅ Done | Multi-step wizard (Employee invite + Owner setup) |
| **Forgot Password Page** | ✅ Done | Email input, success state |
| **Employee Dashboard** | ✅ Done | 4 KPI cards, task list, bar chart, pie chart, AI widget, deadlines |
| **Task Details Page** | ✅ Done | Task info, subtasks, comments (display), AI guidance, attachments, timeline |
| **Submit Progress Page** | ✅ Done | Slider, notes, blockers, file dropzone, submission history |
| **My Evaluations Page** | ✅ Done | Trend chart, evaluation cards, scores breakdown, AI report |
| **Supervisor Dashboard** | ✅ Done | Stats, review queue, team progress, workload chart, alerts, AI summary |
| **Task Review Page** | ✅ Done | Submission view, 4 evaluation sliders, feedback, bug reporting |
| **Executive Dashboard** | ✅ Done | KPIs, department chart, trend line, project status, AI summary, top performers |
| **Reports Page** | ✅ Done | Generator form, chart preview, AI insights, history, scheduled reports |
| **HR Dashboard** | ✅ Done | Stats, engagement chart, heatmap, comm log, warnings, AI insights |
| **Communication Center** | ✅ Done | Compose form, recipient/type selectors, templates, history |
| **Users Management** | ✅ Done | User table, filters, create user modal |
| **AI Control Panel** | ✅ Done | Feature toggles, model config, action logs, usage stats |
| **Role Matrix** | ✅ Done | 19×6 permission grid, toggle cells |
| **Audit Logs** | ✅ Done | Filterable table, type badges, details column |
| **Org Chart** | ✅ Done | CEO card, department tree, members, clickable profiles |
| **Employee Profile** | ✅ Done | Profile card, performance stats, tasks, evaluation history |
| **Notifications** | ✅ Done | Filter, mark read, mark all read, delete |
| **Settings** | ✅ Done | Theme (works), language selector, notification toggles |

#### Architecture Documentation — 100% Complete

| Document | Status | Details |
|---|---|---|
| **AI Architecture Documentation** | ✅ Done | 20 DB tables, 24 pages, role matrix, dashboards, AI agents, workflows, API endpoints |
| **Architecture Diagrams** | ✅ Done | System overview, ER diagram, role flow, AI integration (Mermaid) |
| **UI Generation Prompts** | ✅ Done | 21 detailed Stitch prompts with global design system |

#### Promotional Video (Remotion) — Partially Complete

| Component | Status | Details |
|---|---|---|
| **Video Project Setup** | ✅ Done | Remotion project with React-based scenes |
| **Scene Components** | ✅ Done | 12 scenes: Intro, Problem, Solution, Features, Dashboard, AI, Roles, Evaluation, Stats, Industries, Security, CTA |
| **Theme System** | ✅ Done | Dark theme with brand colors |

---

### 3.2 What is NOT DONE ❌

| Part | Status | Scope |
|---|---|---|
| **Backend API** | ❌ Not Started | Entire C# .NET Web API — 0% |
| **Database** | ❌ Not Started | SQL Server schema, migrations, seeding — 0% |
| **Real Authentication** | ❌ Not Started | JWT, password hashing, refresh tokens — 0% |
| **API Integration in Frontend** | ❌ Not Started | Replace all mock data with API calls — 0% |
| **File Upload** | ❌ Not Started | Actual file storage and retrieval — 0% |
| **AI Agent Endpoints** | ❌ Not Started | External AI API integration — 0% |
| **Real-time Features** | ❌ Not Started | SignalR for notifications, live updates — 0% |
| **PDF Report Generation** | ❌ Not Started | Server-side PDF rendering — 0% |
| **Email/Notification System** | ❌ Not Started | Email sending, push notifications — 0% |
| **Search Functionality** | ❌ Not Started | Global search (TopHeader) is non-functional — 0% |
| **Data Persistence** | ❌ Not Started | All form submissions are no-ops — 0% |
| **Export Features** | ❌ Not Started | CSV/PDF export buttons do nothing — 0% |
| **Internationalization** | ❌ Not Started | Language selector exists but no i18n library — 0% |
| **Testing** | ❌ Not Started | No unit, integration, or E2E tests — 0% |
| **CI/CD Pipeline** | ❌ Not Started | No build, test, or deployment automation — 0% |
| **Production Deployment** | ❌ Not Started | No hosting, domain, SSL setup — 0% |

---

## 4. Implementation Parts Breakdown

---

### PART A: Frontend (React SPA)

**Status:** UI Complete — Needs API Integration  
**Technology:** React 19 + Vite + Tailwind CSS v4 + Recharts + Lucide React  

#### What's Done
- All 34 source files with polished, functional UI components
- Role-based routing and navigation
- Dark/light theme system
- UI component library (11 components)
- Mock data layer for all pages
- Charts and visualizations (bar, line, pie)

#### What's Remaining

| # | Task | Priority | Details |
|---|---|---|---|
| A1 | Install API client library | High | Add `axios` with interceptors for JWT token injection and refresh |
| A2 | Create API service layer | High | Build `/src/services/` with modules: `authService.js`, `projectService.js`, `taskService.js`, `evaluationService.js`, `reportService.js`, `userService.js`, `dashboardService.js`, `notificationService.js`, `aiService.js`, `auditService.js` |
| A3 | Build auth flow | High | Replace demo login with real JWT auth — login, token storage (httpOnly cookie or memory), refresh token rotation, auto-logout on expiry |
| A4 | Implement state management | High | Add Zustand or React Query (TanStack Query) for server state caching, optimistic updates, and background refetching |
| A5 | Replace mock data with API calls | High | Refactor all 20+ pages to fetch from backend API instead of importing from `mockData.js` |
| A6 | Implement form validations | High | Add `react-hook-form` + `zod` for all form pages (SignUp, Create Project, Create Task, Submit Progress, Evaluate, Communication Center, Create User, AI Config, Role Matrix) |
| A7 | Implement file upload | Medium | Wire drag-and-drop on Submit Progress and Task Details to actual upload endpoints (multipart/form-data) |
| A8 | Implement global search | Medium | Wire TopHeader search to backend search API with debounce |
| A9 | Implement real-time notifications | Medium | SignalR client integration for live notification push, toast popups |
| A10 | Implement export functionality | Medium | Wire CSV/PDF export buttons to backend endpoints, trigger browser download |
| A11 | Make subtask toggles functional | Low | Wire subtask checkboxes to PATCH endpoint |
| A12 | Make comments functional | Low | Wire task comments to POST/GET endpoints |
| A13 | Add i18n support | Low | Install `react-i18next`, create language files (EN, AR), wire Settings language selector |
| A14 | Add error boundaries & loading states | Medium | Global error boundary, skeleton loaders on all data pages, toast notification system |
| A15 | Add pagination | Medium | Implement server-side pagination for large lists (tasks, users, audit logs, evaluations) |
| A16 | Responsive design audit | Low | Test and fix all pages for mobile/tablet breakpoints |

#### Recommendations for Part A

| Recommendation | Details |
|---|---|
| **Add TanStack Query (React Query)** | Best-in-class server state management — automatic caching, background refetch, optimistic updates, pagination support. Eliminates manual loading/error state management. |
| **Add `axios` with interceptors** | Central HTTP client with automatic JWT injection, 401 redirect, refresh token handling, request/response logging |
| **Add `react-hook-form` + `zod`** | Performant form management with schema-based validation. Zod schemas can be shared with backend DTOs for type-safe contracts. |
| **Add `react-hot-toast` or `sonner`** | Lightweight toast notification library for success/error feedback on all form submissions |
| **Add `@tanstack/react-table`** | Replace custom DataTable with feature-rich headless table — sorting, filtering, pagination, column resizing built-in |
| **Add error boundary** | Use `react-error-boundary` for graceful crash recovery per-page |
| **Add `date-fns`** | Consistent date formatting across the app instead of raw `Date` |
| **Consider adding Zustand** | Lightweight client state for UI state that doesn't belong in server cache (sidebar open, modals, form drafts) |
| **Add SignalR client** | `@microsoft/signalr` for real-time notification push from backend |

---

### PART B: Backend (C# .NET Web API)

**Status:** ❌ Not Started — 0%  
**Technology:** ASP.NET Core 8.0, Entity Framework Core 8.0, SQL Server 2022  

#### Solution Architecture

```
TaskTune.sln
├── TaskTune.API/                    # ASP.NET Core Web API (entry point)
│   ├── Controllers/                 # API controllers
│   ├── Middlewares/                  # Custom middleware
│   ├── Filters/                     # Action filters
│   └── Program.cs                   # Startup configuration
├── TaskTune.Application/            # Business logic layer
│   ├── Services/                    # Service implementations
│   ├── Interfaces/                  # Service interfaces
│   ├── DTOs/                        # Data Transfer Objects
│   ├── Validators/                  # FluentValidation validators
│   └── Mappings/                    # AutoMapper profiles
├── TaskTune.Domain/                 # Domain entities & enums
│   ├── Entities/                    # EF Core entity classes
│   ├── Enums/                       # Role, Status, Priority enums
│   └── Interfaces/                  # Repository interfaces
├── TaskTune.Infrastructure/         # Data access & external services
│   ├── Data/                        # DbContext, configurations
│   ├── Repositories/                # Repository implementations
│   ├── Services/                    # External service clients (AI, email, storage)
│   └── Migrations/                  # EF Core migrations
└── TaskTune.Tests/                  # Unit & integration tests
    ├── Unit/
    └── Integration/
```

#### Backend — Complete Task List

| # | Task | Priority | Details |
|---|---|---|---|
| **Project Setup** ||||
| B1 | Create .NET 8 solution with Clean Architecture | High | 5 projects: API, Application, Domain, Infrastructure, Tests |
| B2 | Configure NuGet packages | High | EF Core, JWT, BCrypt, AutoMapper, FluentValidation, Serilog, Polly, Swashbuckle |
| B3 | Configure `Program.cs` startup | High | DI registration, middleware pipeline, CORS for React frontend, JWT config, Swagger |
| B4 | Set up `appsettings.json` | High | Connection strings, JWT settings, AI API keys, file storage config, CORS origins |
| **Database & EF Core** ||||
| B5 | Create all 20 entity classes | High | Company, Department, Position, User, RolePermission, Project, Task, TaskAccessControl, Subtask, TaskDependency, TaskProgress, TaskAttachment, Evaluation, Bug, Notification, CommunicationLog, AuditLog, AIActionLog, Report, IndustryTemplate |
| B6 | Configure EF Core fluent relationships | High | All foreign keys, cascading rules, indexes, unique constraints |
| B7 | Create DbContext with all DbSets | High | `TaskTuneDbContext` with 20 DbSets |
| B8 | Create initial migration | High | `dotnet ef migrations add InitialCreate` |
| B9 | Create database seeding service | Medium | Seed roles, default admin, sample industry templates |
| **Authentication & Authorization** ||||
| B10 | Implement JWT authentication | High | Login, token generation (access + refresh), token validation |
| B11 | Implement password hashing | High | BCrypt.Net-Next for secure password storage |
| B12 | Implement refresh token rotation | High | Secure refresh token stored in DB, rotate on each use |
| B13 | Build role-based authorization middleware | High | Custom `[Authorize(Roles = "...")]` + resource-level permissions via `task_access_control` |
| B14 | Implement forgot-password workflow | Medium | Generate reset token, notify admin/technical team |
| **DTOs & Mapping** ||||
| B15 | Create request/response DTOs for all entities | High | Separate Create, Update, Response DTOs for each entity |
| B16 | Configure AutoMapper profiles | High | Entity ↔ DTO mapping for all 20 entities |
| B17 | Create FluentValidation validators | High | Validate all Create/Update DTOs |
| **API Controllers** ||||
| B18 | `AuthController` | High | POST login, POST forgot-password, POST refresh-token, POST logout |
| B19 | `ProjectsController` | High | CRUD + list with filters + team member assignment |
| B20 | `TasksController` | High | CRUD + list with filters + assign + progress submit + access control |
| B21 | `SubtasksController` | High | CRUD for subtasks within a task |
| B22 | `EvaluationsController` | High | Create, update, list by user, trigger AI evaluation |
| B23 | `UsersController` | High | CRUD + list with filters + deactivate + password reset |
| B24 | `DashboardsController` | High | Employee / Supervisor / Manager / Executive / HR / Admin KPIs |
| B25 | `NotificationsController` | Medium | List, mark read, mark all read, delete |
| B26 | `ReportsController` | Medium | Generate, list, download PDF |
| B27 | `CommunicationsController` | Medium | Create/send, list history |
| B28 | `AuditLogsController` | Medium | List with filters, export CSV |
| B29 | `DepartmentsController` | Medium | CRUD for departments, hierarchy |
| B30 | `PositionsController` | Medium | CRUD for positions, reporting lines |
| B31 | `RolePermissionsController` | Medium | Get matrix, update permissions (CEO only) |
| B32 | `AIController` | Medium | Proxy to AI agents, configuration endpoints |
| B33 | `CompaniesController` | Medium | Company settings, industry config |
| B34 | `BugsController` | Medium | CRUD for bugs within task review |
| B35 | `FileUploadController` | Medium | Upload, download, delete attachments |
| **Business Services** ||||
| B36 | `AuthService` | High | Login logic, token generation, password validation |
| B37 | `ProjectService` | High | Project CRUD with authorization checks |
| B38 | `TaskService` | High | Task CRUD, assignment, progress tracking, access control checks |
| B39 | `EvaluationService` | High | Evaluation CRUD, score calculation, AI trigger |
| B40 | `UserService` | High | User CRUD, profile, deactivation |
| B41 | `DashboardService` | High | KPI aggregation per role: queries, calculations, chart data |
| B42 | `NotificationService` | Medium | Create, list, mark read, push via SignalR |
| B43 | `ReportService` | Medium | Report generation, PDF creation, storage |
| B44 | `CommunicationService` | Medium | Message creation, recipient resolution, template application |
| B45 | `AuditService` | Medium | Centralized audit logging for all sensitive operations |
| B46 | `AIIntegrationService` | Medium | HTTP client for all 5 AI agents, retry logic, caching |
| B47 | `FileStorageService` | Medium | File upload/download to local storage or Azure Blob |
| **Real-Time Features** ||||
| B48 | Configure SignalR Hub | Medium | `NotificationHub` for real-time notification push |
| B49 | Implement SignalR authentication | Medium | JWT-based SignalR auth for authenticated connections |
| **Cross-Cutting Concerns** ||||
| B50 | Global exception handling middleware | High | Catch unhandled exceptions, return structured error responses |
| B51 | Request logging middleware | Medium | Serilog structured logging for all requests |
| B52 | Rate limiting middleware | Medium | Per-company and per-user rate limits |
| B53 | CORS configuration | High | Allow React frontend origin |
| B54 | API versioning | Low | URL-based versioning (`/api/v1/`) |

#### Backend — Entity Classes (Detailed)

```csharp
// Domain/Entities/Company.cs
public class Company
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string IndustryPrimary { get; set; } = string.Empty;
    public string? IndustrySecondary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? SettingsJson { get; set; }  // JSONB

    // Navigation
    public ICollection<Department> Departments { get; set; } = [];
    public ICollection<Position> Positions { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<CommunicationLog> CommunicationLogs { get; set; } = [];
    public ICollection<Report> Reports { get; set; } = [];
}

// Domain/Entities/User.cs
public class User
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PositionId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Department? Department { get; set; }
    public Position? Position { get; set; }
    public ICollection<TaskItem> AssignedTasks { get; set; } = [];
    public ICollection<TaskItem> CreatedTasks { get; set; } = [];
    public ICollection<Evaluation> EvaluationsReceived { get; set; } = [];
    public ICollection<Evaluation> EvaluationsGiven { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}

// Domain/Enums/UserRole.cs
public enum UserRole
{
    Employee = 0,
    Supervisor = 1,
    Manager = 2,
    CEO = 3,
    HR = 4,
    Admin = 5
}

// Domain/Enums/TaskStatus.cs
public enum TaskStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Blocked = 3
}

// Domain/Enums/Priority.cs
public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

// Domain/Enums/ProjectStatus.cs
public enum ProjectStatus
{
    Planning = 0,
    Active = 1,
    Completed = 2,
    OnHold = 3
}
```

#### Backend — Sample Controller Structure

```csharp
// API/Controllers/AuthController.cs
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // POST /api/auth/login            → LoginDto → TokenResponse
    // POST /api/auth/forgot-password  → ForgotPasswordDto → MessageResponse
    // POST /api/auth/refresh-token    → RefreshTokenDto → TokenResponse
    // POST /api/auth/logout           → [Authorize] → MessageResponse
}

// API/Controllers/TasksController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    // GET    /api/tasks                    → TaskFilterDto → PagedResult<TaskResponse>
    // POST   /api/tasks                    → CreateTaskDto → TaskResponse
    // GET    /api/tasks/{id}               → Guid → TaskDetailResponse
    // PUT    /api/tasks/{id}               → UpdateTaskDto → TaskResponse
    // DELETE /api/tasks/{id}               → Guid → NoContent
    // POST   /api/tasks/{id}/progress      → SubmitProgressDto → ProgressResponse
    // POST   /api/tasks/{id}/access        → SetAccessDto → AccessResponse
    // GET    /api/tasks/{id}/subtasks      → Guid → List<SubtaskResponse>
    // PATCH  /api/tasks/{id}/subtasks/{sid}→ UpdateSubtaskDto → SubtaskResponse
}

// API/Controllers/DashboardsController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardsController : ControllerBase
{
    // GET /api/dashboards/employee     → [Authorize(Roles="Employee,Supervisor,Manager,CEO")]
    // GET /api/dashboards/supervisor   → [Authorize(Roles="Supervisor,Manager,CEO")]
    // GET /api/dashboards/manager      → [Authorize(Roles="Manager,CEO")]
    // GET /api/dashboards/executive    → [Authorize(Roles="CEO")]
    // GET /api/dashboards/hr           → [Authorize(Roles="HR")]
    // GET /api/dashboards/admin        → [Authorize(Roles="Admin")]
}
```

#### Backend — NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | 8.x | ORM |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.x | SQL Server provider |
| `Microsoft.EntityFrameworkCore.Tools` | 8.x | Migrations CLI |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.x | JWT authentication |
| `BCrypt.Net-Next` | 4.x | Password hashing |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | 12.x | Object mapping |
| `FluentValidation.AspNetCore` | 11.x | Request validation |
| `Serilog.AspNetCore` | 8.x | Structured logging |
| `Serilog.Sinks.Console` | 5.x | Console log sink |
| `Serilog.Sinks.File` | 5.x | File log sink |
| `Polly` | 8.x | Retry policies & circuit breakers for AI API calls |
| `Swashbuckle.AspNetCore` | 6.x | Swagger/OpenAPI documentation |
| `Microsoft.AspNetCore.SignalR` | (built-in) | Real-time notifications |
| `QuestPDF` | 2024.x | PDF report generation |
| `CsvHelper` | 33.x | CSV export |
| `Azure.Storage.Blobs` | 12.x | (Optional) Azure Blob Storage |

#### Recommendations for Part B

| Recommendation | Details |
|---|---|
| **Use Clean Architecture (Onion)** | Separate API, Application, Domain, Infrastructure layers for testability and maintainability |
| **Use Repository + Unit of Work pattern** | Abstract data access, enable unit testing with mock repositories |
| **Use MediatR for CQRS** | (Optional) Separate read/write operations with command/query handlers — beneficial for complex business logic like dashboard aggregation |
| **Use FluentValidation** | Declarative validation rules for all DTOs, auto-wired to ASP.NET model binding |
| **Use AutoMapper** | Eliminate manual entity↔DTO mapping boilerplate |
| **Use Serilog** | Structured logging with multiple sinks (console, file, Seq, Application Insights) |
| **Use Polly for AI calls** | Retry with exponential backoff + circuit breaker for the 5 AI agent HTTP calls |
| **Use QuestPDF** | Modern, fluent API for server-side PDF report generation (open-source, no license cost for community) |
| **Use API versioning** | `Asp.Versioning.Http` package — URL segment versioning `/api/v1/` for future-proofing |
| **Use Response caching** | Cache dashboard KPI queries (30-60 second TTL) to reduce database load |
| **Use Health checks** | `Microsoft.AspNetCore.Diagnostics.HealthChecks` + `AspNetCore.HealthChecks.SqlServer` for monitoring |
| **Consider adding Hangfire** | Background job processing for: scheduled reports, automated reminders, email sending, AI batch processing |
| **Consider adding MassTransit** | (Future) Message bus for event-driven architecture — decouple audit logging, notifications, AI triggers |

---

### PART C: Database (SQL Server)

**Status:** ❌ Not Started — 0%  
**Technology:** SQL Server 2022 (or SQL Server Express for development)

> Full schema specification in [Section 7](#7-detailed-database-specification)

#### What Needs to Be Done

| # | Task | Priority | Details |
|---|---|---|---|
| C1 | Install SQL Server 2022 (Developer/Express) | High | Local development instance |
| C2 | Create `TaskTuneDb` database | High | Initial database creation |
| C3 | Define all 20 tables via EF Core migrations | High | Code-first approach from entity classes |
| C4 | Configure indexes | High | PKs, FKs, composite indexes, filtered indexes |
| C5 | Configure constraints | High | Unique (email), check (score 0-100), default values |
| C6 | Create seed data migration | Medium | Default admin user, sample company, industry templates |
| C7 | Create stored procedures for complex reports | Medium | Dashboard KPI aggregations, performance calculations |
| C8 | Configure backup strategy | Low | Automated daily full + 15-min log backups |
| C9 | Set up connection pooling | Medium | Configure in connection string |
| C10 | Create database documentation | Low | Generate ER diagram from DB |

#### Recommendations for Part C

| Recommendation | Details |
|---|---|
| **Use SQL Server 2022 Developer Edition** | Free for development, full feature set including JSON support, temporal tables |
| **Use SSMS (SQL Server Management Studio)** | Primary database management tool |
| **Use Azure Data Studio** | Lightweight alternative, cross-platform, Jupyter notebook support for ad-hoc queries |
| **Use Temporal Tables** | SQL Server 2022 feature — automatic audit trail for `Users`, `Tasks`, `Evaluations` tables. Keeps history of all row changes with zero application code. |
| **Use Column-Level Encryption** | Encrypt `PasswordHash`, sensitive evaluation feedback using Always Encrypted |
| **Use Row-Level Security (RLS)** | Enforce company_id isolation at the database level for multi-tenant security |
| **Add computed columns** | `FullName` as computed column on Users (`FirstName + ' ' + LastName`) |
| **Consider Partitioning** | Partition `AuditLogs` and `AIActionsLog` by month for performance as data grows |
| **Use Database Project (.sqlproj)** | (Optional) SQL Server Data Tools project for schema-as-code versioning alongside the .NET solution |

---

### PART D: AI Agents Integration

**Status:** ❌ Not Started — 0%  
**Technology:** External API (HTTP REST), integrated via `IHttpClientFactory`

> Full specification in [Section 8](#8-detailed-ai-agents-specification)

#### What Needs to Be Done

| # | Task | Priority | Details |
|---|---|---|---|
| D1 | Choose and provision AI API provider | High | OpenAI GPT-4 / Azure OpenAI / Anthropic Claude — choose one |
| D2 | Design prompt templates for each agent | High | System prompts + user prompt templates for all 5 agents |
| D3 | Implement `AIIntegrationService` | High | Central service with typed HTTP client and retry logic |
| D4 | Implement Chatbot Agent endpoint | Medium | Stream-style response, conversation history, context awareness |
| D5 | Implement Task Guidance Agent endpoint | High | Task data → structured guidance response |
| D6 | Implement Progress Tracking Agent endpoint | Medium | Historical data → prediction + risk assessment |
| D7 | Implement Email/Logs Agent endpoint | Medium | Context → drafted communication |
| D8 | Implement Feedback/Reports Agent endpoint | High | Scores + history → detailed feedback + report insights |
| D9 | Build prompt chaining / context enrichment | Medium | Fetch relevant DB data before passing to AI |
| D10 | Implement response caching | Medium | Cache task guidance (5-min TTL), dashboard summaries (15-min TTL) |
| D11 | Build AI action logging | Medium | Log all inputs/outputs to `ai_actions_log` table |
| D12 | Implement rate limiting per company | Medium | 100 req/min/company configurable |
| D13 | Build fallback for API failures | Medium | Cached responses, graceful degradation messages |
| D14 | Create AI admin configuration API | Low | Toggle features, change model params via AI Control Panel |

#### Recommendations for Part D

| Recommendation | Details |
|---|---|
| **Use Azure OpenAI Service** | Enterprise-grade, data remains in your Azure tenant, SLA guarantees, content filtering built-in, same GPT-4 models |
| **Use Semantic Kernel** | Microsoft .NET SDK for AI orchestration — supports OpenAI, Azure OpenAI, Hugging Face. Handles prompt templates, plugins, and function calling natively in C# |
| **Use structured outputs (JSON mode)** | Force AI responses into typed JSON schemas matching your DTOs — eliminates parsing errors |
| **Implement prompt versioning** | Store prompts in DB or config files, version them, A/B test prompt variations |
| **Add confidence scores** | Include confidence scoring in AI responses, flag low-confidence results for human review |
| **Consider LangChain.NET** | Alternative to Semantic Kernel — more community plugins, RAG patterns |
| **Add vector embeddings** | (Future) Embed task descriptions, evaluations for semantic search and smarter AI context retrieval |
| **Consider fine-tuning** | (Future) Fine-tune on company-specific evaluation data for more accurate industry-specific feedback |

---

### PART E: File Storage & Infrastructure

**Status:** ❌ Not Started — 0%

#### What Needs to Be Done

| # | Task | Priority | Details |
|---|---|---|---|
| E1 | Implement local file storage service | High | For development — store in `wwwroot/uploads/` with structured folders |
| E2 | Implement file upload endpoint | High | Multipart form-data, size limits (50MB), MIME type validation |
| E3 | Implement file download endpoint | High | Authenticated download with access control check |
| E4 | Implement Azure Blob Storage service | Low | (Optional) For production — same interface, cloud storage backend |
| E5 | Configure file security | High | Validate file types (no executables), scan for malware, restrict paths |
| E6 | Implement Redis caching | Medium | Cache dashboard KPIs, frequently accessed data, session data |
| E7 | Configure SMTP for email | Medium | SendGrid or Azure Communication Services for email delivery |

#### Recommendations for Part E

| Recommendation | Details |
|---|---|
| **Use Azure Blob Storage** | Scalable, cost-effective, geo-redundant file storage with SAS tokens for secure access |
| **Use Redis (StackExchange.Redis)** | In-memory cache for dashboard queries, session tokens, rate limit counters |
| **Use SendGrid** | Reliable email delivery with templates, analytics, .NET SDK |
| **Antivirus scanning** | Integrate ClamAV or Azure Defender for Storage to scan uploaded files |
| **CDN for static files** | Azure CDN or CloudFlare for frontend static assets in production |
| **Implement pre-signed URLs** | Generate time-limited download URLs for file attachments instead of proxy downloads |

---

### PART F: DevOps & Deployment

**Status:** ❌ Not Started — 0%

> Full specification in [Section 10](#10-detailed-devops-specification)

#### What Needs to Be Done

| # | Task | Priority | Details |
|---|---|---|---|
| F1 | Set up Git repository structure | High | Monorepo or separate repos for frontend + backend |
| F2 | Configure ESLint + Prettier for frontend | Medium | Code quality enforcement |
| F3 | Configure .editorconfig + analyzers for backend | Medium | Roslyn analyzers, code style rules |
| F4 | Create Docker configuration | Medium | `Dockerfile` for API, `docker-compose.yml` for local dev (API + SQL Server + Redis) |
| F5 | Create CI pipeline (GitHub Actions) | Medium | Build → Test → Lint → Deploy for both frontend and backend |
| F6 | Create CD pipeline | Low | Automated deployment to staging/production |
| F7 | Configure environment management | Medium | Development, Staging, Production configurations |
| F8 | Set up monitoring & alerting | Low | Health checks, performance monitoring, error tracking |

#### Recommendations for Part F

| Recommendation | Details |
|---|---|
| **GitHub Actions** | Best CI/CD for GitHub-hosted repos. Free tier for public repos, generous minutes for private. |
| **Docker Compose for local dev** | Single `docker-compose up` spins up API + SQL Server + Redis — eliminates "works on my machine" issues |
| **Azure App Service** | PaaS deployment for .NET API — auto-scale, managed TLS, deployment slots for zero-downtime releases |
| **Vercel or Azure Static Web Apps** | Host React SPA — automatic builds from GitHub, global CDN, custom domain + SSL |
| **SonarQube or SonarCloud** | Code quality gate — detect bugs, vulnerabilities, code smells automatically in CI |
| **Seq or Application Insights** | Centralized structured log viewer for Serilog output |
| **Consider Terraform** | If deploying to Azure, use Terraform or Bicep for infrastructure-as-code |

---

### PART G: Testing & QA

**Status:** ❌ Not Started — 0%

> Full specification in [Section 11](#11-detailed-testing-specification)

#### What Needs to Be Done

| # | Task | Priority | Details |
|---|---|---|---|
| G1 | Set up backend unit test project | High | xUnit + Moq + FluentAssertions |
| G2 | Write unit tests for all services | High | Target 80% code coverage on business logic |
| G3 | Write integration tests for API controllers | High | `WebApplicationFactory<Program>`, in-memory SQL Server |
| G4 | Set up frontend testing | Medium | Vitest + React Testing Library |
| G5 | Write component tests for UI library | Medium | 11 UI components |
| G6 | Write page-level tests for critical flows | Medium | Login, task creation, progress submission, evaluation |
| G7 | Set up E2E testing | Low | Playwright for critical user journeys |
| G8 | Performance/load testing | Low | k6 or JMeter for API load testing |

#### Recommendations for Part G

| Recommendation | Details |
|---|---|
| **xUnit** | Standard .NET testing framework, excellent async support, parallel execution |
| **Moq + FluentAssertions** | Clean mocking + readable assertion syntax for C# unit tests |
| **Testcontainers** | Spin up real SQL Server in Docker containers for integration tests — more realistic than in-memory DB |
| **Vitest** | Vite-native test runner — fastest setup for Vite projects, compatible with Jest API |
| **React Testing Library** | User-behavior-focused testing, avoids testing implementation details |
| **Playwright** | Microsoft's E2E framework — cross-browser, auto-wait, codegen for test recording |
| **Stryker.NET** | Mutation testing to verify test quality — ensures tests catch real bugs, not just code coverage vanity metrics |

---

### PART H: Promotional Video (Remotion)

**Status:** ~60% Complete  
**Technology:** Remotion (React-based video generation)

#### What's Done
- Video project setup with Remotion
- 12 scene components (Intro, Problem, Solution, Features, Dashboard, AI, Roles, Evaluation, Stats, Industries, Security, CTA)
- Dark theme and brand styling

#### What's Remaining

| # | Task | Priority | Details |
|---|---|---|---|
| H1 | Review and polish animations | Low | Ensure smooth transitions between scenes |
| H2 | Add voiceover/narration | Low | Generate or record narration audio |
| H3 | Render final video | Low | `npx remotion render` to MP4 |
| H4 | Add background music | Low | Royalty-free track |
| H5 | Create thumbnail | Low | Eye-catching preview frame |

#### Recommendations for Part H

| Recommendation | Details |
|---|---|
| **Use ElevenLabs** | AI-generated voiceover matching professional tone |
| **Add real dashboard screenshots** | Once frontend is connected to API, capture real screenshots for the video |
| **Export at 1080p and 4K** | Multiple resolutions for different platforms |
| **Create short version (30s)** | Social media cut alongside the full-length video |

---

## 5. Detailed Backend Specification

### 5.1 API Endpoints — Complete Reference

#### Authentication API

| Method | Endpoint | Auth | Request Body | Response | Description |
|---|---|---|---|---|---|
| POST | `/api/auth/login` | No | `{ email, password }` | `{ accessToken, refreshToken, user }` | Authenticate user, return JWT |
| POST | `/api/auth/forgot-password` | No | `{ email }` | `{ message }` | Submit reset request to admin |
| POST | `/api/auth/refresh-token` | No | `{ refreshToken }` | `{ accessToken, refreshToken }` | Get new access token |
| POST | `/api/auth/logout` | Yes | — | `{ message }` | Invalidate refresh token |

#### Projects API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/projects` | Yes | Manager, CEO, Admin | List projects (with pagination, filters) |
| POST | `/api/projects` | Yes | Manager, CEO, Admin | Create project |
| GET | `/api/projects/{id}` | Yes | Varies | Get project details |
| PUT | `/api/projects/{id}` | Yes | Manager, CEO, Admin | Update project |
| DELETE | `/api/projects/{id}` | Yes | Manager, CEO, Admin | Soft delete project |
| GET | `/api/projects/{id}/tasks` | Yes | Varies | List project tasks |

#### Tasks API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/tasks` | Yes | All | List tasks (filtered by role/access) |
| POST | `/api/tasks` | Yes | Supervisor+ | Create task |
| GET | `/api/tasks/{id}` | Yes | Access-controlled | Get task details |
| PUT | `/api/tasks/{id}` | Yes | Access-controlled | Update task |
| DELETE | `/api/tasks/{id}` | Yes | Creator/Admin | Soft delete task |
| POST | `/api/tasks/{id}/progress` | Yes | Assignee | Submit progress update |
| GET | `/api/tasks/{id}/progress` | Yes | Access-controlled | List progress submissions |
| POST | `/api/tasks/{id}/access` | Yes | Creator/Admin | Set task access permissions |
| GET | `/api/tasks/{id}/subtasks` | Yes | Access-controlled | List subtasks |
| PATCH | `/api/tasks/{id}/subtasks/{subtaskId}` | Yes | Assignee | Toggle subtask status |
| GET | `/api/tasks/{id}/attachments` | Yes | Access-controlled | List attachments |
| POST | `/api/tasks/{id}/attachments` | Yes | Assignee | Upload attachment |
| GET | `/api/tasks/{id}/dependencies` | Yes | Access-controlled | List dependencies |

#### Evaluations API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| POST | `/api/evaluations` | Yes | Supervisor+ | Create evaluation |
| GET | `/api/evaluations/user/{userId}` | Yes | Self/Supervisor+ | List user evaluations |
| GET | `/api/evaluations/{id}` | Yes | Access-controlled | Get evaluation details |
| PUT | `/api/evaluations/{id}` | Yes | Evaluator | Update evaluation |
| POST | `/api/evaluations/{id}/trigger-ai` | Yes | Supervisor+ | Generate AI feedback |

#### Users API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/users` | Yes | Admin, HR, Manager+ | List users (filtered by role) |
| POST | `/api/users` | Yes | Admin | Create user |
| GET | `/api/users/{id}` | Yes | Self/Manager+ | Get user profile |
| PUT | `/api/users/{id}` | Yes | Self/Admin | Update user |
| DELETE | `/api/users/{id}` | Yes | Admin | Deactivate user (soft) |
| POST | `/api/users/{id}/reset-password` | Yes | Admin | Reset user password |

#### Dashboards API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/dashboards/employee` | Yes | Employee+ | Employee KPIs & chart data |
| GET | `/api/dashboards/supervisor` | Yes | Supervisor+ | Team KPIs & chart data |
| GET | `/api/dashboards/manager` | Yes | Manager+ | Department KPIs & chart data |
| GET | `/api/dashboards/executive` | Yes | CEO | Company-wide KPIs & chart data |
| GET | `/api/dashboards/hr` | Yes | HR | Engagement KPIs & chart data |
| GET | `/api/dashboards/admin` | Yes | Admin | System health KPIs |

#### Reports API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| POST | `/api/reports/generate` | Yes | Supervisor+ | Generate new report |
| GET | `/api/reports` | Yes | Supervisor+ | List generated reports |
| GET | `/api/reports/{id}/download` | Yes | Report owner+ | Download report PDF |

#### Notifications API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/notifications` | Yes | All | List user notifications |
| PUT | `/api/notifications/{id}/mark-read` | Yes | Owner | Mark single as read |
| POST | `/api/notifications/mark-all-read` | Yes | Owner | Mark all as read |
| DELETE | `/api/notifications/{id}` | Yes | Owner | Delete notification |

#### Communications API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| POST | `/api/communications` | Yes | HR | Send communication |
| GET | `/api/communications` | Yes | HR | List communication history |
| GET | `/api/communications/templates` | Yes | HR | List industry templates |

#### Audit Logs API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/audit-logs` | Yes | Manager+, Admin | List with filters & pagination |
| GET | `/api/audit-logs/export` | Yes | Admin | Export filtered logs as CSV |

#### Organization API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/departments` | Yes | All | List departments (tree structure) |
| POST | `/api/departments` | Yes | Admin/CEO | Create department |
| PUT | `/api/departments/{id}` | Yes | Admin/CEO | Update department |
| GET | `/api/positions` | Yes | All | List positions |
| POST | `/api/positions` | Yes | Admin/CEO | Create position |
| PUT | `/api/positions/{id}` | Yes | Admin/CEO | Update position |
| GET | `/api/org-chart` | Yes | Varies | Get org chart tree |

#### Role Permissions API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/role-permissions` | Yes | CEO, Admin | Get role permission matrix |
| PUT | `/api/role-permissions` | Yes | CEO | Update role permissions |

#### AI API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| POST | `/api/ai/chatbot` | Yes | All | Chat with AI assistant |
| POST | `/api/ai/task-guidance` | Yes | Employee+ | Generate task guidance |
| POST | `/api/ai/progress-tracking` | Yes | Supervisor+ | Analyze progress |
| POST | `/api/ai/email-generation` | Yes | HR | Generate communication |
| POST | `/api/ai/evaluation-feedback` | Yes | Supervisor+ | Generate evaluation feedback |
| POST | `/api/ai/report-insights` | Yes | Manager+ | Generate report insights |
| GET | `/api/ai/config` | Yes | Admin, CEO | Get AI configuration |
| PUT | `/api/ai/config` | Yes | Admin, CEO | Update AI configuration |
| GET | `/api/ai/logs` | Yes | Admin | Get AI action logs |

#### Bugs API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| POST | `/api/tasks/{taskId}/bugs` | Yes | Supervisor+ | Report bug |
| GET | `/api/tasks/{taskId}/bugs` | Yes | Access-controlled | List task bugs |
| PUT | `/api/bugs/{id}` | Yes | Reporter | Update bug status |

#### File Upload API

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| POST | `/api/files/upload` | Yes | All | Upload file (multipart) |
| GET | `/api/files/{id}` | Yes | Access-controlled | Download file |
| DELETE | `/api/files/{id}` | Yes | Uploader/Admin | Delete file |

### 5.2 DTO Examples

```csharp
// Application/DTOs/Auth/LoginRequestDto.cs
public record LoginRequestDto(string Email, string Password);

// Application/DTOs/Auth/TokenResponseDto.cs
public record TokenResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserResponseDto User
);

// Application/DTOs/Task/CreateTaskDto.cs
public record CreateTaskDto(
    Guid ProjectId,
    string TaskName,
    string Description,
    Priority Priority,
    Guid AssignedTo,
    DateTime StartDate,
    DateTime DueDate,
    string? SubmitMethod,
    string? DefinitionOfDone,
    List<CreateSubtaskDto>? Subtasks,
    List<Guid>? DependsOnTaskIds,
    TaskAccessControlDto? AccessControl
);

// Application/DTOs/Task/TaskResponseDto.cs
public record TaskResponseDto(
    Guid TaskId,
    Guid ProjectId,
    string ProjectName,
    string TaskName,
    string Description,
    Priority Priority,
    TaskStatus Status,
    UserSummaryDto AssignedTo,
    UserSummaryDto CreatedBy,
    DateTime StartDate,
    DateTime DueDate,
    int CompletionPercentage,
    string? SubmitMethod,
    string? DefinitionOfDone,
    int SubtaskCount,
    int SubtaskCompletedCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// Application/DTOs/Dashboard/EmployeeDashboardDto.cs
public record EmployeeDashboardDto(
    int TasksCompleted,
    int TotalTasks,
    int ActiveTasks,
    decimal AverageScore,
    decimal ScoreChange,
    int UpcomingDeadlines,
    int OverdueTasks,
    List<TaskSummaryDto> MyTasks,
    List<ChartDataPointDto> CompletionTrend,
    List<StatusDistributionDto> StatusDistribution,
    AiGuidanceDto? AiGuidance,
    List<NotificationSummaryDto> RecentNotifications
);

// Application/DTOs/Evaluation/CreateEvaluationDto.cs
public record CreateEvaluationDto(
    Guid TaskId,
    Guid EvaluatedUserId,
    decimal OverallScore,
    decimal QualityScore,
    decimal TimelinessScore,
    decimal CollaborationScore,
    string RiskLevel,
    string? ManualFeedback
);

// Application/DTOs/Common/PagedResultDto.cs
public record PagedResultDto<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);
```

### 5.3 Middleware Pipeline

```
Request Pipeline (in order):
──────────────────────────────────
1. Exception Handling Middleware     ← Catches all unhandled exceptions, returns structured error
2. Serilog Request Logging          ← Logs method, path, status code, duration
3. CORS Middleware                   ← Allow React frontend origin
4. Rate Limiting Middleware          ← Per-company / per-user rate limits
5. Authentication Middleware         ← JWT validation
6. Authorization Middleware          ← Role-based + resource-level checks
7. Audit Logging Action Filter      ← Auto-log sensitive operations
8. Controller Action                 ← Business logic execution
──────────────────────────────────
```

### 5.4 JWT Token Configuration

```json
// appsettings.json — JWT Section
{
  "JwtSettings": {
    "SecretKey": "GENERATE-256-BIT-SECRET-KEY-HERE",
    "Issuer": "TaskTuneAPI",
    "Audience": "TaskTuneFrontend",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

### 5.5 Background Jobs (Recommended: Hangfire)

| Job | Schedule | Description |
|---|---|---|
| `SendTaskDeadlineReminders` | Daily at 8:00 AM | Notify users of tasks due within 24/48/72 hours |
| `FlagOverdueTasks` | Hourly | Mark tasks past due date, notify supervisors |
| `GenerateWeeklyExecutiveSummary` | Weekly (Monday 7:00 AM) | AI-generated executive summary for CEO |
| `GenerateScheduledReports` | Per configuration | Auto-generate recurring reports |
| `CleanExpiredRefreshTokens` | Daily at 2:00 AM | Remove expired refresh tokens from DB |
| `ArchiveOldAuditLogs` | Monthly | Move logs older than 2 years to archive |
| `SendAutomatedHRAlerts` | Daily | Flag employees with repeated delays to HR |

---

## 6. Detailed Frontend Specification

### 6.1 New npm Packages to Add

```json
{
  "dependencies": {
    "axios": "^1.7.0",
    "@tanstack/react-query": "^5.60.0",
    "@microsoft/signalr": "^8.0.0",
    "react-hook-form": "^7.54.0",
    "zod": "^3.24.0",
    "@hookform/resolvers": "^3.9.0",
    "zustand": "^5.0.0",
    "sonner": "^1.7.0",
    "date-fns": "^4.1.0",
    "react-i18next": "^15.2.0",
    "i18next": "^24.2.0"
  },
  "devDependencies": {
    "vitest": "^2.2.0",
    "@testing-library/react": "^16.3.0",
    "@testing-library/jest-dom": "^6.6.0"
  }
}
```

### 6.2 New File Structure

```
src/
├── services/                    # API service layer (NEW)
│   ├── api.js                   # Axios instance with interceptors
│   ├── authService.js           # Login, logout, refresh
│   ├── projectService.js        # Project CRUD
│   ├── taskService.js           # Task CRUD, progress, subtasks
│   ├── evaluationService.js     # Evaluation CRUD, AI trigger
│   ├── userService.js           # User CRUD, profile
│   ├── dashboardService.js      # Dashboard KPI endpoints
│   ├── reportService.js         # Generate, list, download
│   ├── notificationService.js   # List, mark read
│   ├── communicationService.js  # Send, list
│   ├── auditService.js          # List, export
│   ├── aiService.js             # AI agent endpoints
│   ├── orgService.js            # Departments, positions, org chart
│   └── fileService.js           # Upload, download
├── hooks/                       # Custom React hooks (NEW)
│   ├── useAuth.js               # Auth state + actions
│   ├── useTasks.js              # TanStack Query hooks for tasks
│   ├── useProjects.js           # TanStack Query hooks for projects
│   ├── useDashboard.js          # TanStack Query hooks for dashboards
│   └── useNotifications.js      # Real-time notification hook
├── stores/                      # Zustand stores (NEW)
│   ├── uiStore.js               # Sidebar, modals, search state
│   └── notificationStore.js     # Real-time notification state
├── lib/                         # Utilities (NEW)
│   ├── queryClient.js           # TanStack Query client config
│   ├── signalr.js               # SignalR connection setup
│   └── validators.js            # Shared Zod schemas
├── i18n/                        # Internationalization (NEW)
│   ├── index.js                 # i18next config
│   ├── en.json                  # English translations
│   └── ar.json                  # Arabic translations
└── (existing files unchanged)
```

### 6.3 API Service Example

```javascript
// src/services/api.js
import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'https://localhost:7001/api',
  headers: { 'Content-Type': 'application/json' },
});

// Request interceptor — inject JWT
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Response interceptor — handle 401, refresh token
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      // Attempt token refresh, retry request, or redirect to login
    }
    return Promise.reject(error);
  }
);

export default api;
```

### 6.4 Frontend-to-Backend Integration Map

| Frontend Page | API Endpoints Required | Current Data Source |
|---|---|---|
| Login | `POST /api/auth/login` | None (demo) |
| SignUp | `POST /api/auth/register` or `POST /api/users` | None (navigate only) |
| Forgot Password | `POST /api/auth/forgot-password` | None (UI flip) |
| Employee Dashboard | `GET /api/dashboards/employee` | `mockData.js` |
| Task Details | `GET /api/tasks/{id}`, subtasks, attachments | `mockData.js` |
| Submit Progress | `POST /api/tasks/{id}/progress`, `POST /api/files/upload` | None (no-op) |
| My Evaluations | `GET /api/evaluations/user/{id}` | `mockData.js` |
| Supervisor Dashboard | `GET /api/dashboards/supervisor` | `mockData.js` |
| Task Review | `GET /api/tasks/{id}/progress`, `POST /api/evaluations`, `POST /api/tasks/{id}/bugs` | None (no-op) |
| Executive Dashboard | `GET /api/dashboards/executive` | `mockData.js` |
| Reports | `POST /api/reports/generate`, `GET /api/reports`, download | `mockData.js` |
| HR Dashboard | `GET /api/dashboards/hr` | `mockData.js` |
| Communication Center | `POST /api/communications`, `GET /api/communications` | None (no-op) |
| Projects | CRUD `GET/POST/PUT/DELETE /api/projects` | `mockData.js` |
| Tasks Management | CRUD `GET/POST/PUT/DELETE /api/tasks` | `mockData.js` |
| Users Management | CRUD `GET/POST/PUT/DELETE /api/users` | `mockData.js` |
| AI Control Panel | `GET/PUT /api/ai/config`, `GET /api/ai/logs` | Local state |
| Role Matrix | `GET/PUT /api/role-permissions` | Local state |
| Audit Logs | `GET /api/audit-logs`, `GET /api/audit-logs/export` | `mockData.js` |
| Org Chart | `GET /api/org-chart` | `mockData.js` |
| Employee Profile | `GET /api/users/{id}` (detailed) | `mockData.js` |
| Notifications | `GET /api/notifications`, PUT mark-read | `mockData.js` |
| Settings | `PUT /api/users/{id}/settings` | Local state |

---

## 7. Detailed Database Specification

### 7.1 Complete Table Definitions with SQL Server Types

#### Table 1: Companies

```sql
CREATE TABLE Companies (
    CompanyId        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyName      NVARCHAR(255)    NOT NULL,
    IndustryPrimary  NVARCHAR(100)    NOT NULL,
    IndustrySecondary NVARCHAR(100)   NULL,
    CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    SettingsJson     NVARCHAR(MAX)    NULL  -- JSON
);
```

#### Table 2: Departments

```sql
CREATE TABLE Departments (
    DepartmentId       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyId          UNIQUEIDENTIFIER NOT NULL REFERENCES Companies(CompanyId),
    DepartmentName     NVARCHAR(255)    NOT NULL,
    ParentDepartmentId UNIQUEIDENTIFIER NULL REFERENCES Departments(DepartmentId),
    Description        NVARCHAR(MAX)    NULL,
    CreatedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Departments_CompanyId NONCLUSTERED (CompanyId),
    INDEX IX_Departments_ParentId NONCLUSTERED (ParentDepartmentId)
);
```

#### Table 3: Positions

```sql
CREATE TABLE Positions (
    PositionId           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyId            UNIQUEIDENTIFIER NOT NULL REFERENCES Companies(CompanyId),
    DepartmentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Departments(DepartmentId),
    PositionTitle        NVARCHAR(255)    NOT NULL,
    HierarchyLevel       INT              NOT NULL,
    Responsibilities     NVARCHAR(MAX)    NULL,
    ReportsToPositionId  UNIQUEIDENTIFIER NULL REFERENCES Positions(PositionId),
    CreatedAt            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Positions_CompanyId NONCLUSTERED (CompanyId),
    INDEX IX_Positions_DepartmentId NONCLUSTERED (DepartmentId),
    INDEX IX_Positions_ReportsTo NONCLUSTERED (ReportsToPositionId)
);
```

#### Table 4: Users

```sql
CREATE TABLE Users (
    UserId          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyId       UNIQUEIDENTIFIER NOT NULL REFERENCES Companies(CompanyId),
    DepartmentId    UNIQUEIDENTIFIER NULL REFERENCES Departments(DepartmentId),
    PositionId      UNIQUEIDENTIFIER NULL REFERENCES Positions(PositionId),
    Email           NVARCHAR(255)    NOT NULL,
    PasswordHash    NVARCHAR(255)    NOT NULL,
    FirstName       NVARCHAR(100)    NOT NULL,
    LastName        NVARCHAR(100)    NOT NULL,
    Phone           NVARCHAR(20)     NULL,
    Role            TINYINT          NOT NULL,  -- 0=Employee,1=Supervisor,2=Manager,3=CEO,4=HR,5=Admin
    IsActive        BIT              NOT NULL DEFAULT 1,
    LastLogin       DATETIME2        NULL,
    RefreshToken    NVARCHAR(500)    NULL,
    RefreshTokenExpiry DATETIME2     NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FullName        AS (FirstName + N' ' + LastName) PERSISTED,
    
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    INDEX IX_Users_CompanyId NONCLUSTERED (CompanyId),
    INDEX IX_Users_DepartmentId NONCLUSTERED (DepartmentId),
    INDEX IX_Users_Role NONCLUSTERED (Role),
    INDEX IX_Users_Email NONCLUSTERED (Email),
    INDEX IX_Users_IsActive NONCLUSTERED (IsActive) WHERE IsActive = 1
);
```

#### Table 5: RolePermissions

```sql
CREATE TABLE RolePermissions (
    PermissionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyId    UNIQUEIDENTIFIER NOT NULL REFERENCES Companies(CompanyId),
    Role         TINYINT          NOT NULL,
    Resource     NVARCHAR(100)    NOT NULL,  -- 'tasks', 'projects', 'users', etc.
    Action       NVARCHAR(50)     NOT NULL,  -- 'create', 'read', 'update', 'delete'
    CanPerform   BIT              NOT NULL DEFAULT 0,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_RolePermissions UNIQUE (CompanyId, Role, Resource, Action),
    INDEX IX_RolePermissions_CompanyRole NONCLUSTERED (CompanyId, Role)
);
```

#### Table 6: Projects

```sql
CREATE TABLE Projects (
    ProjectId   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyId   UNIQUEIDENTIFIER NOT NULL REFERENCES Companies(CompanyId),
    ProjectName NVARCHAR(255)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    ManagerId   UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    Status      TINYINT          NOT NULL DEFAULT 0,  -- 0=Planning,1=Active,2=Completed,3=OnHold
    StartDate   DATE             NOT NULL,
    EndDate     DATE             NOT NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Projects_CompanyId NONCLUSTERED (CompanyId),
    INDEX IX_Projects_ManagerId NONCLUSTERED (ManagerId),
    INDEX IX_Projects_Status NONCLUSTERED (Status),
    CONSTRAINT CK_Projects_Dates CHECK (EndDate >= StartDate)
);
```

#### Table 7: Tasks

```sql
CREATE TABLE Tasks (
    TaskId               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ProjectId            UNIQUEIDENTIFIER NOT NULL REFERENCES Projects(ProjectId),
    TaskName             NVARCHAR(255)    NOT NULL,
    Description          NVARCHAR(MAX)    NULL,
    Priority             TINYINT          NOT NULL DEFAULT 1,  -- 0=Low,1=Medium,2=High,3=Critical
    Status               TINYINT          NOT NULL DEFAULT 0,  -- 0=NotStarted,1=InProgress,2=Completed,3=Blocked
    AssignedTo           UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    CreatedBy            UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    StartDate            DATE             NOT NULL,
    DueDate              DATE             NOT NULL,
    CompletionPercentage INT              NOT NULL DEFAULT 0,
    SubmitMethod         NVARCHAR(255)    NULL,
    DefinitionOfDone     NVARCHAR(MAX)    NULL,
    CreatedAt            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Tasks_ProjectId NONCLUSTERED (ProjectId),
    INDEX IX_Tasks_AssignedTo NONCLUSTERED (AssignedTo),
    INDEX IX_Tasks_CreatedBy NONCLUSTERED (CreatedBy),
    INDEX IX_Tasks_Status NONCLUSTERED (Status),
    INDEX IX_Tasks_Priority NONCLUSTERED (Priority),
    INDEX IX_Tasks_DueDate NONCLUSTERED (DueDate),
    INDEX IX_Tasks_StatusDueDate NONCLUSTERED (Status, DueDate) INCLUDE (TaskName, Priority, AssignedTo),
    CONSTRAINT CK_Tasks_CompletionPercentage CHECK (CompletionPercentage BETWEEN 0 AND 100),
    CONSTRAINT CK_Tasks_Dates CHECK (DueDate >= StartDate)
);
```

#### Table 8: TaskAccessControl

```sql
CREATE TABLE TaskAccessControl (
    AccessId          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TaskId            UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId) ON DELETE CASCADE,
    UserId            UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    CanView           BIT NOT NULL DEFAULT 0,
    CanEdit           BIT NOT NULL DEFAULT 0,
    CanViewEvaluation BIT NOT NULL DEFAULT 0,
    CanEditEvaluation BIT NOT NULL DEFAULT 0,
    GrantedBy         UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    CreatedAt         DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_TaskAccess_TaskUser UNIQUE (TaskId, UserId),
    INDEX IX_TaskAccess_UserId NONCLUSTERED (UserId),
    INDEX IX_TaskAccess_TaskId NONCLUSTERED (TaskId)
);
```

#### Table 9: Subtasks

```sql
CREATE TABLE Subtasks (
    SubtaskId   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TaskId      UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId) ON DELETE CASCADE,
    SubtaskName NVARCHAR(255)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    Status      TINYINT          NOT NULL DEFAULT 0,  -- 0=NotStarted,1=InProgress,2=Completed
    OrderIndex  INT              NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Subtasks_TaskId NONCLUSTERED (TaskId)
);
```

#### Table 10: TaskDependencies

```sql
CREATE TABLE TaskDependencies (
    DependencyId     UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TaskId           UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId),
    DependsOnTaskId  UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId),
    DependencyType   TINYINT          NOT NULL DEFAULT 0,  -- 0=FinishToStart,1=StartToStart
    CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_TaskDep UNIQUE (TaskId, DependsOnTaskId),
    CONSTRAINT CK_TaskDep_NoCycle CHECK (TaskId <> DependsOnTaskId),
    INDEX IX_TaskDep_TaskId NONCLUSTERED (TaskId),
    INDEX IX_TaskDep_DependsOn NONCLUSTERED (DependsOnTaskId)
);
```

#### Table 11: TaskProgress

```sql
CREATE TABLE TaskProgress (
    ProgressId           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TaskId               UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId),
    SubmittedBy          UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    CompletionPercentage INT              NOT NULL,
    Notes                NVARCHAR(MAX)    NULL,
    Blockers             NVARCHAR(MAX)    NULL,
    SubmittedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_TaskProgress_TaskId NONCLUSTERED (TaskId),
    INDEX IX_TaskProgress_SubmittedBy NONCLUSTERED (SubmittedBy),
    CONSTRAINT CK_TaskProgress_Completion CHECK (CompletionPercentage BETWEEN 0 AND 100)
);
```

#### Table 12: TaskAttachments

```sql
CREATE TABLE TaskAttachments (
    AttachmentId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TaskId       UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId),
    ProgressId   UNIQUEIDENTIFIER NULL REFERENCES TaskProgress(ProgressId),
    UploadedBy   UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    FileName     NVARCHAR(255)    NOT NULL,
    FilePath     NVARCHAR(500)    NOT NULL,
    FileSize     INT              NOT NULL,
    MimeType     NVARCHAR(100)    NOT NULL,
    UploadedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_TaskAttachments_TaskId NONCLUSTERED (TaskId),
    INDEX IX_TaskAttachments_ProgressId NONCLUSTERED (ProgressId)
);
```

#### Table 13: Evaluations

```sql
CREATE TABLE Evaluations (
    EvaluationId       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TaskId             UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId),
    EvaluatedUserId    UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    EvaluatorId        UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    OverallScore       DECIMAL(5,2)     NOT NULL,
    QualityScore       DECIMAL(5,2)     NOT NULL,
    TimelinessScore    DECIMAL(5,2)     NOT NULL,
    CollaborationScore DECIMAL(5,2)     NOT NULL,
    RiskLevel          TINYINT          NOT NULL DEFAULT 0,  -- 0=Low,1=Medium,2=High
    ManualFeedback     NVARCHAR(MAX)    NULL,
    AiGeneratedFeedback NVARCHAR(MAX)   NULL,
    Status             TINYINT          NOT NULL DEFAULT 0,  -- 0=Draft,1=Submitted,2=Reviewed
    SubmittedAt        DATETIME2        NULL,
    CreatedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Evaluations_TaskId NONCLUSTERED (TaskId),
    INDEX IX_Evaluations_EvaluatedUserId NONCLUSTERED (EvaluatedUserId),
    INDEX IX_Evaluations_EvaluatorId NONCLUSTERED (EvaluatorId),
    CONSTRAINT CK_Evaluations_Overall CHECK (OverallScore BETWEEN 0 AND 100),
    CONSTRAINT CK_Evaluations_Quality CHECK (QualityScore BETWEEN 0 AND 100),
    CONSTRAINT CK_Evaluations_Timeliness CHECK (TimelinessScore BETWEEN 0 AND 100),
    CONSTRAINT CK_Evaluations_Collaboration CHECK (CollaborationScore BETWEEN 0 AND 100)
);
```

#### Table 14: Bugs

```sql
CREATE TABLE Bugs (
    BugId          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TaskId         UNIQUEIDENTIFIER NOT NULL REFERENCES Tasks(TaskId),
    ReportedBy     UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    BugDescription NVARCHAR(MAX)    NOT NULL,
    RiskLevel      TINYINT          NOT NULL DEFAULT 0,  -- 0=Low,1=Medium,2=High
    Status         TINYINT          NOT NULL DEFAULT 0,  -- 0=Open,1=InProgress,2=Resolved
    CreatedAt      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAt     DATETIME2        NULL,
    
    INDEX IX_Bugs_TaskId NONCLUSTERED (TaskId),
    INDEX IX_Bugs_ReportedBy NONCLUSTERED (ReportedBy),
    INDEX IX_Bugs_Status NONCLUSTERED (Status)
);
```

#### Table 15: Notifications

```sql
CREATE TABLE Notifications (
    NotificationId   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId           UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    NotificationType TINYINT          NOT NULL,  -- 0=Reminder,1=Overdue,2=Evaluation,3=System
    Title            NVARCHAR(255)    NOT NULL,
    Message          NVARCHAR(MAX)    NOT NULL,
    RelatedTaskId    UNIQUEIDENTIFIER NULL REFERENCES Tasks(TaskId),
    RelatedProjectId UNIQUEIDENTIFIER NULL REFERENCES Projects(ProjectId),
    IsRead           BIT              NOT NULL DEFAULT 0,
    SentAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ReadAt           DATETIME2        NULL,
    
    INDEX IX_Notifications_UserId NONCLUSTERED (UserId),
    INDEX IX_Notifications_UserUnread NONCLUSTERED (UserId, IsRead) WHERE IsRead = 0,
    INDEX IX_Notifications_SentAt NONCLUSTERED (SentAt DESC)
);
```

#### Table 16: CommunicationLog

```sql
CREATE TABLE CommunicationLog (
    CommunicationId       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyId             UNIQUEIDENTIFIER NOT NULL REFERENCES Companies(CompanyId),
    SentBy                UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    RecipientUserId       UNIQUEIDENTIFIER NULL REFERENCES Users(UserId),
    RecipientDepartmentId UNIQUEIDENTIFIER NULL REFERENCES Departments(DepartmentId),
    CommunicationType     TINYINT          NOT NULL,  -- 0=Announcement,1=Reminder,2=Alert
    Subject               NVARCHAR(255)    NOT NULL,
    Body                  NVARCHAR(MAX)    NOT NULL,
    SentAt                DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_CommLog_CompanyId NONCLUSTERED (CompanyId),
    INDEX IX_CommLog_SentBy NONCLUSTERED (SentBy),
    INDEX IX_CommLog_SentAt NONCLUSTERED (SentAt DESC)
);
```

#### Table 17: AuditLogs

```sql
CREATE TABLE AuditLogs (
    LogId        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId       UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    Action       NVARCHAR(100)    NOT NULL,
    ResourceType NVARCHAR(100)    NOT NULL,
    ResourceId   UNIQUEIDENTIFIER NOT NULL,
    ChangesJson  NVARCHAR(MAX)    NULL,  -- JSON before/after
    IpAddress    NVARCHAR(45)     NULL,
    UserAgent    NVARCHAR(500)    NULL,
    Timestamp    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_AuditLogs_UserId NONCLUSTERED (UserId),
    INDEX IX_AuditLogs_Timestamp NONCLUSTERED (Timestamp DESC),
    INDEX IX_AuditLogs_ResourceType NONCLUSTERED (ResourceType),
    INDEX IX_AuditLogs_Action NONCLUSTERED (Action)
);
```

#### Table 18: AIActionsLog

```sql
CREATE TABLE AIActionsLog (
    AiLogId       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ActionType    TINYINT          NOT NULL,  -- 0=Guidance,1=Evaluation,2=Summary,3=Insight
    TaskId        UNIQUEIDENTIFIER NULL REFERENCES Tasks(TaskId),
    UserId        UNIQUEIDENTIFIER NULL REFERENCES Users(UserId),
    InputData     NVARCHAR(MAX)    NULL,  -- JSON
    OutputData    NVARCHAR(MAX)    NULL,  -- JSON
    ModelVersion  NVARCHAR(50)     NULL,
    Timestamp     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_AIActionsLog_TaskId NONCLUSTERED (TaskId),
    INDEX IX_AIActionsLog_UserId NONCLUSTERED (UserId),
    INDEX IX_AIActionsLog_Timestamp NONCLUSTERED (Timestamp DESC),
    INDEX IX_AIActionsLog_ActionType NONCLUSTERED (ActionType)
);
```

#### Table 19: Reports

```sql
CREATE TABLE Reports (
    ReportId          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    CompanyId         UNIQUEIDENTIFIER NOT NULL REFERENCES Companies(CompanyId),
    GeneratedBy       UNIQUEIDENTIFIER NOT NULL REFERENCES Users(UserId),
    ReportType        TINYINT          NOT NULL,  -- 0=Performance,1=Department,2=User,3=Project
    ReportPeriodStart DATE             NOT NULL,
    ReportPeriodEnd   DATE             NOT NULL,
    FilePath          NVARCHAR(500)    NULL,
    MetadataJson      NVARCHAR(MAX)    NULL,  -- JSON
    GeneratedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Reports_CompanyId NONCLUSTERED (CompanyId),
    INDEX IX_Reports_GeneratedBy NONCLUSTERED (GeneratedBy),
    INDEX IX_Reports_GeneratedAt NONCLUSTERED (GeneratedAt DESC)
);
```

#### Table 20: IndustryTemplates

```sql
CREATE TABLE IndustryTemplates (
    TemplateId   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    IndustryName NVARCHAR(100)    NOT NULL,
    TemplateType TINYINT          NOT NULL,  -- 0=Communication,1=Evaluation,2=Report
    Content      NVARCHAR(MAX)    NOT NULL,  -- JSON
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_IndustryTemplates_Industry NONCLUSTERED (IndustryName)
);
```

### 7.2 Database Relationship Summary

| Parent Table | Child Table | FK Column | Cascade Delete |
|---|---|---|---|
| Companies | Departments | CompanyId | No (restrict) |
| Companies | Positions | CompanyId | No (restrict) |
| Companies | Users | CompanyId | No (restrict) |
| Companies | Projects | CompanyId | No (restrict) |
| Companies | RolePermissions | CompanyId | Cascade |
| Companies | CommunicationLog | CompanyId | No (restrict) |
| Companies | Reports | CompanyId | No (restrict) |
| Departments | Departments (self) | ParentDepartmentId | Set Null |
| Departments | Positions | DepartmentId | No (restrict) |
| Departments | Users | DepartmentId | Set Null |
| Positions | Users | PositionId | Set Null |
| Positions | Positions (self) | ReportsToPositionId | Set Null |
| Users | Projects | ManagerId | No (restrict) |
| Users | Tasks | AssignedTo | No (restrict) |
| Users | Tasks | CreatedBy | No (restrict) |
| Users | Evaluations | EvaluatedUserId | No (restrict) |
| Users | Evaluations | EvaluatorId | No (restrict) |
| Users | AuditLogs | UserId | No (restrict) |
| Users | Notifications | UserId | Cascade |
| Projects | Tasks | ProjectId | No (restrict) |
| Tasks | Subtasks | TaskId | Cascade |
| Tasks | TaskAccessControl | TaskId | Cascade |
| Tasks | TaskProgress | TaskId | No (restrict) |
| Tasks | TaskAttachments | TaskId | No (restrict) |
| Tasks | TaskDependencies | TaskId | Cascade |
| Tasks | Evaluations | TaskId | No (restrict) |
| Tasks | Bugs | TaskId | No (restrict) |
| TaskProgress | TaskAttachments | ProgressId | Set Null |

### 7.3 Indexing Strategy

| Category | Index | Columns | Purpose |
|---|---|---|---|
| **Unique** | UQ_Users_Email | Email | Prevent duplicate emails |
| **Unique** | UQ_RolePermissions | CompanyId, Role, Resource, Action | One permission per role/resource/action |
| **Unique** | UQ_TaskAccess_TaskUser | TaskId, UserId | One access record per user per task |
| **Unique** | UQ_TaskDep | TaskId, DependsOnTaskId | Prevent duplicate dependencies |
| **Composite** | IX_Tasks_StatusDueDate | Status, DueDate INCLUDE(TaskName, Priority, AssignedTo) | Dashboard queries — overdue tasks |
| **Filtered** | IX_Users_IsActive | IsActive WHERE IsActive = 1 | Active user queries only |
| **Filtered** | IX_Notifications_UserUnread | UserId, IsRead WHERE IsRead = 0 | Unread notification count |
| **Descending** | IX_AuditLogs_Timestamp | Timestamp DESC | Recent audit logs |
| **Descending** | IX_Notifications_SentAt | SentAt DESC | Recent notifications |

### 7.4 Seed Data

```sql
-- Default Admin User (password: Admin@123 — BCrypt hashed)
INSERT INTO Users (UserId, CompanyId, Email, PasswordHash, FirstName, LastName, Role, IsActive)
VALUES (NEWID(), @CompanyId, 'admin@tasktune.ai', '$2a$11$...hashed...', 'System', 'Admin', 5, 1);

-- Industry Templates
INSERT INTO IndustryTemplates (TemplateId, IndustryName, TemplateType, Content)
VALUES
    (NEWID(), 'Technology', 0, '{"greeting":"Dear {name}","deadline_reminder":"..."}'),
    (NEWID(), 'Healthcare', 0, '{"greeting":"Dear {name}","deadline_reminder":"..."}'),
    (NEWID(), 'Finance', 0, '{"greeting":"Dear {name}","deadline_reminder":"..."}'),
    (NEWID(), 'Education', 0, '{"greeting":"Dear {name}","deadline_reminder":"..."}'),
    (NEWID(), 'Manufacturing', 0, '{"greeting":"Dear {name}","deadline_reminder":"..."}');

-- Default Role Permissions (per company)
-- CEO: full access to everything
-- Admin: system management access
-- HR: employee data + communication access
-- Manager: department + project access
-- Supervisor: team + task review access
-- Employee: own tasks + evaluations access
```

---

## 8. Detailed AI Agents Specification

### 8.1 Agent Architecture

```
┌──────────────────────────────────────────────────┐
│                  AIController                     │
│                 /api/ai/*                         │
├──────────────────────────────────────────────────┤
│              AIIntegrationService                 │
│  ┌─────────────┬──────────────┬────────────────┐ │
│  │ PromptBuilder│ HttpClient   │ ResponseParser │ │
│  │ (Templates)  │ (Polly retry)│ (JSON → DTO)  │ │
│  └─────────────┴──────────────┴────────────────┘ │
├──────────────────────────────────────────────────┤
│         AI Provider (Azure OpenAI / OpenAI)       │
│  ┌─────────────────────────────────────────────┐ │
│  │  GPT-4 / GPT-4o / Claude 3.5 Sonnet        │ │
│  └─────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────┘
```

### 8.2 Prompt Templates (Per Agent)

#### Agent 1: Chatbot
```
System: You are TaskTune AI assistant for a {industry} company. The user is a {role} named {name}.
They are currently on the {page} page. Provide helpful guidance related to the TaskTune platform.
Keep responses concise, professional, and actionable. If they ask about a specific task or project,
reference the provided context data.

Context: {contextual_data_from_current_page}
Conversation History: {last_5_messages}
User Message: {user_message}
```

#### Agent 2: Task Guidance
```
System: You are a task guidance AI for a {industry} company. Analyze the following task and provide
structured guidance for the assigned {role}. Consider the task requirements, subtasks, dependencies,
deadline, and the user's role. Provide actionable steps, estimated time, risk factors, and resources.

Task: {task_name}
Description: {description}
Subtasks: {subtasks_json}
Definition of Done: {definition_of_done}
Due Date: {due_date}
Dependencies: {dependencies_json}
Industry: {company_industry}

Respond in JSON format: { guidance_text, suggested_subtasks[], estimated_time, resources[], risk_factors[] }
```

#### Agent 3: Progress Tracking
```
System: Analyze the progress of a task and provide predictive insights. Consider submission history,
blockers, velocity, and the deadline to assess risk and predict completion.

Task: {task_name}
Completion: {completion_percentage}%
Submissions: {progress_history_json}
Blockers: {blockers_json}
Due Date: {due_date}
User Performance History: {performance_history_json}

Respond in JSON: { progress_summary, completion_prediction, blockers_analysis, recommendations[], risk_level }
```

#### Agent 4: Email/Communication
```
System: Generate a professional {communication_type} for a {industry} company. The tone should be
{tone}. Personalize for the recipient and include relevant task/project references.

Recipient: {recipient_name}, {recipient_role}
Type: {announcement|reminder|alert}
Context: {context_data_json}
Template Preference: {template_name}

Respond in JSON: { subject, body, tone, suggested_send_time }
```

#### Agent 5: Feedback & Reports
```
System: Generate detailed evaluation feedback for an employee in a {industry} company. Analyze their
scores, supervisor feedback, task history, and provide constructive, actionable feedback.

Scores: Overall={overall}, Quality={quality}, Timeliness={timeliness}, Collaboration={collaboration}
Supervisor Feedback: {manual_feedback}
Task: {task_details_json}
Performance History: {past_evaluations_json}
Industry: {company_industry}

Respond in JSON: { ai_feedback, strengths[], improvement_areas[], recommended_training[], overall_assessment }
```

### 8.3 AI Configuration Schema (stored in Companies.SettingsJson)

```json
{
  "ai": {
    "provider": "azure-openai",
    "apiKey": "encrypted_key",
    "endpoint": "https://your-instance.openai.azure.com/",
    "model": "gpt-4o",
    "temperature": 0.7,
    "maxTokens": 2000,
    "features": {
      "chatbot": true,
      "taskGuidance": true,
      "progressTracking": true,
      "emailGeneration": true,
      "feedbackReports": true
    },
    "rateLimit": {
      "requestsPerMinute": 100,
      "tokensPerDay": 500000
    },
    "caching": {
      "taskGuidanceTtlMinutes": 5,
      "dashboardSummaryTtlMinutes": 15
    }
  }
}
```

---

## 9. Detailed Infrastructure Specification

### 9.1 Development Environment

| Component | Technology | Details |
|---|---|---|
| **API Runtime** | .NET 8 SDK | Kestrel web server |
| **Database** | SQL Server 2022 Developer | Free, full-featured |
| **Cache** | Redis (Docker) | `redis:latest` container |
| **Frontend Dev Server** | Vite | Hot module replacement |
| **IDE** | Visual Studio 2022 / VS Code | C# Dev Kit for VS Code |
| **DB Management** | SSMS / Azure Data Studio | Database administration |
| **API Testing** | Postman / Bruno | Manual API testing |

### 9.2 Production Architecture (Recommended)

```
┌───────────────────┐     ┌───────────────────┐
│  Vercel / Azure   │     │  Azure App Service │
│  Static Web Apps  │────▶│  (.NET 8 API)      │
│  (React Frontend) │     │  + SignalR Hub      │
└───────────────────┘     └─────────┬──────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
              ┌─────┴─────┐  ┌─────┴─────┐  ┌─────┴──────┐
              │ Azure SQL  │  │   Redis   │  │ Azure Blob │
              │  Database  │  │   Cache   │  │  Storage   │
              └───────────┘  └───────────┘  └────────────┘
                                    │
                          ┌─────────┴─────────┐
                          │  Azure OpenAI     │
                          │  (AI Agents)      │
                          └───────────────────┘
```

### 9.3 Docker Compose (Development)

```yaml
# docker-compose.yml
version: '3.8'
services:
  api:
    build: ./tasktune-backend
    ports: ["7001:8080"]
    depends_on: [sqlserver, redis]
    environment:
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=TaskTuneDb;User=sa;Password=Your_password123;TrustServerCertificate=True
      - Redis__ConnectionString=redis:6379

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports: ["1433:1433"]
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=Your_password123
    volumes:
      - sqlserver-data:/var/opt/mssql

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]

volumes:
  sqlserver-data:
```

---

## 10. Detailed DevOps Specification

### 10.1 Git Branching Strategy

```
main              ← Production releases (protected)
├── develop       ← Integration branch
│   ├── feature/BE-auth          ← Backend: Authentication
│   ├── feature/BE-tasks-api     ← Backend: Tasks API
│   ├── feature/FE-api-integration ← Frontend: API integration
│   ├── feature/AI-agents        ← AI agent integration
│   └── bugfix/login-redirect    ← Bug fixes
└── release/v1.0  ← Release candidates
```

### 10.2 CI Pipeline (GitHub Actions)

```yaml
# .github/workflows/ci.yml
name: CI Pipeline
on: [push, pull_request]
jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: cd tasktune-frontend && npm ci
      - run: cd tasktune-frontend && npm run lint
      - run: cd tasktune-frontend && npm run build
      - run: cd tasktune-frontend && npm run test
```

### 10.3 Environment Configuration

| Setting | Development | Staging | Production |
|---|---|---|---|
| **API URL** | `https://localhost:7001` | `https://staging-api.tasktune.ai` | `https://api.tasktune.ai` |
| **SQL Server** | `localhost,1433` | Azure SQL (Standard) | Azure SQL (Premium) |
| **Redis** | `localhost:6379` | Azure Cache for Redis (Basic) | Azure Cache for Redis (Standard) |
| **File Storage** | Local `wwwroot/uploads/` | Azure Blob (LRS) | Azure Blob (GRS) |
| **AI API** | OpenAI (dev key) | Azure OpenAI (staging) | Azure OpenAI (production) |
| **Logging** | Console + File | Seq + Application Insights | Application Insights |
| **CORS** | `http://localhost:5173` | `https://staging.tasktune.ai` | `https://tasktune.ai` |

---

## 11. Detailed Testing Specification

### 11.1 Backend Unit Tests

| Service | Test Count (Est.) | Key Test Cases |
|---|---|---|
| AuthService | 12 | Login success, invalid credentials, inactive user, token generation, refresh rotation, password hashing |
| TaskService | 20 | Create task, assign, update status, submit progress, access control, overdue detection |
| EvaluationService | 10 | Create evaluation, score validation (0-100), AI trigger, status transitions |
| DashboardService | 15 | KPI calculations per role, empty data handling, date range filtering |
| UserService | 10 | Create, deactivate, role validation, email uniqueness |
| ProjectService | 8 | Create, update status, manager assignment, date validation |
| NotificationService | 6 | Create, mark read, bulk operations |
| ReportService | 6 | Generate, PDF creation, download authorization |

### 11.2 Backend Integration Tests

| Controller | Test Count (Est.) | Key Test Cases |
|---|---|---|
| AuthController | 8 | Full login flow, invalid creds return 401, refresh works, logout invalidates token |
| TasksController | 15 | CRUD flow, role-based filtering, access control enforcement, progress submission |
| EvaluationsController | 8 | Create/read lifecycle, score persistence, AI trigger |
| UsersController | 8 | Admin-only creation, role enforcement, profile access |
| DashboardsController | 6 | Correct KPIs per role, unauthorized role returns 403 |

### 11.3 Frontend Tests

| Category | Test Count (Est.) | Tools |
|---|---|---|
| UI Component Tests | 15 | Vitest + React Testing Library |
| Auth Flow Tests | 5 | Vitest + MSW (Mock Service Worker) |
| Dashboard Rendering | 6 | Vitest + React Testing Library |
| Form Validation | 10 | Vitest + React Testing Library |
| E2E Critical Paths | 8 | Playwright |

### 11.4 E2E Test Scenarios

| # | Scenario | Steps |
|---|---|---|
| 1 | Employee Task Lifecycle | Login → View Dashboard → Open Task → Submit Progress → View updated status |
| 2 | Supervisor Review | Login → View Dashboard → Open Review → Submit Evaluation → Employee sees result |
| 3 | Admin User Management | Login → Create User → Verify user appears in list → Deactivate user |
| 4 | CEO Reports | Login → Executive Dashboard → Generate Report → Download PDF |
| 5 | HR Communication | Login → Communication Center → Compose → Send → Verify in history |

---

## 12. Detailed Video Specification

### 12.1 Current Scenes

| # | Scene | Duration (est.) | Status |
|---|---|---|---|
| 1 | IntroScene | 5s | ✅ Built |
| 2 | ProblemScene | 8s | ✅ Built |
| 3 | SolutionScene | 8s | ✅ Built |
| 4 | FeaturesScene | 10s | ✅ Built |
| 5 | DashboardScene | 10s | ✅ Built |
| 6 | AIScene | 10s | ✅ Built |
| 7 | RolesScene | 8s | ✅ Built |
| 8 | EvaluationScene | 8s | ✅ Built |
| 9 | StatsScene | 6s | ✅ Built |
| 10 | IndustriesScene | 6s | ✅ Built |
| 11 | SecurityScene | 6s | ✅ Built |
| 12 | CTAScene | 5s | ✅ Built |

### 12.2 Remaining Work

- Animation polish and timing adjustments
- Voiceover audio generation (ElevenLabs or similar)
- Background music integration
- Final render to MP4 (1080p, 4K)
- Thumbnail generation
- 30-second social media cut

---

## 13. General Recommendations

### 13.1 Architecture Recommendations

| # | Recommendation | Priority | Impact |
|---|---|---|---|
| 1 | **Use Clean Architecture** for backend — separate API, Application, Domain, Infrastructure projects | High | Testability, maintainability, separation of concerns |
| 2 | **Use the Repository + Unit of Work pattern** — abstract EF Core behind interfaces | High | Enables unit testing without database, swappable data layer |
| 3 | **Add a Shared Contracts library** — DTOs/models shared between frontend and backend teams | Medium | Prevents contract drift, improves API integration speed |
| 4 | **Implement CQRS for dashboards** — separate read models from write operations | Medium | Dashboard queries can be optimized independently from write operations |
| 5 | **Use database views** for complex dashboard queries — pre-join common aggregations | Medium | Reduces query complexity in application code |
| 6 | **Implement multi-tenancy at the database level** — all queries filtered by CompanyId | High | Critical for SaaS — prevents data leakage between companies |

### 13.2 Security Recommendations

| # | Recommendation | Priority | Impact |
|---|---|---|---|
| 1 | **Store JWT in httpOnly cookies** (not localStorage) — prevents XSS token theft | High | Security |
| 2 | **Implement CSRF protection** — anti-forgery tokens for cookie-based auth | High | Security |
| 3 | **Add IP-based anomaly detection** — alert on login from new location/device | Medium | Security |
| 4 | **Implement account lockout** — lock after 5 failed login attempts for 15 minutes | High | Security |
| 5 | **Add audit log tamper detection** — hash chain on audit logs to detect modifications | Low | Compliance |
| 6 | **Use Azure Key Vault** (production) — store secrets, connection strings, API keys outside code | High | Security |
| 7 | **Implement data encryption at rest** — Transparent Data Encryption (TDE) on SQL Server | Medium | Compliance |
| 8 | **Add Content Security Policy headers** — prevent XSS, clickjacking | High | Security |

### 13.3 Performance Recommendations

| # | Recommendation | Priority | Impact |
|---|---|---|---|
| 1 | **Cache dashboard KPIs** — Redis with 30-60 second TTL | High | Dashboard load time |
| 2 | **Use database read replicas** for reporting queries | Low | Report generation speed at scale |
| 3 | **Implement pagination everywhere** — 25 items per page default | High | Frontend/API performance |
| 4 | **Use EF Core compiled queries** for hot-path dashboard queries | Medium | Database performance |
| 5 | **Lazy load frontend routes** — React.lazy() for all page components | Medium | Initial bundle size |
| 6 | **Add ETags** for API responses — conditional GET avoids re-sending unchanged data | Low | Network efficiency |
| 7 | **Use SQL Server columnstore indexes** on AuditLogs — efficient aggregation analytics | Low | Audit log query performance |

### 13.4 UX Recommendations

| # | Recommendation | Priority | Impact |
|---|---|---|---|
| 1 | **Add skeleton loading states** — shimmer placeholders on all data pages | Medium | Perceived performance |
| 2 | **Add optimistic updates** — update UI immediately, rollback on failure | Medium | Responsiveness |
| 3 | **Add keyboard shortcuts** — `Ctrl+K` for global search, `N` for new task, etc. | Low | Power user productivity |
| 4 | **Add drag-and-drop for task boards** — Kanban view in addition to list view | Low | Task management UX |
| 5 | **Add dark/light mode auto-detection** — already done, but sync with system changes | ✅ Done | — |
| 6 | **Add onboarding tour** — first-time user guided walkthrough using `react-joyride` | Low | User adoption |

### 13.5 Business Recommendations

| # | Recommendation | Priority | Impact |
|---|---|---|---|
| 1 | **Multi-tenant pricing model** — Free tier (10 users), Pro (50 users), Enterprise (unlimited) | Future | Revenue |
| 2 | **API documentation portal** — Published Swagger/OpenAPI spec for enterprise integrations | Medium | Developer experience |
| 3 | **Webhook support** — Allow companies to subscribe to events (task.completed, evaluation.created) | Future | Integrations |
| 4 | **Mobile app** — React Native or Flutter companion app for on-the-go task management | Future | User reach |
| 5 | **SSO integration** — SAML 2.0 / OpenID Connect for enterprise customers | Future | Enterprise sales |
| 6 | **Slack/Teams integration** — Bot notifications and commands within messaging platforms | Future | User engagement |

---

## 14. Priority & Phasing Plan

### Phase 1: Foundation (Weeks 1-4) — Backend Core + Database

| Priority | Tasks | Part |
|---|---|---|
| P0 | Set up .NET 8 solution with Clean Architecture | B |
| P0 | Create all 20 entity classes + DbContext + initial migration | B + C |
| P0 | Implement JWT authentication (login, refresh, logout) | B |
| P0 | Implement Users CRUD API | B |
| P0 | Implement role-based authorization middleware | B |
| P0 | Create seed data (admin user, default permissions) | C |
| P0 | Configure Swagger documentation | B |
| P1 | Implement Projects CRUD API | B |
| P1 | Implement Tasks CRUD API (with subtasks, dependencies) | B |
| P1 | Implement task access control API | B |

### Phase 2: Core Features (Weeks 5-8) — Business Logic + Frontend Integration

| Priority | Tasks | Part |
|---|---|---|
| P0 | Implement progress submission API | B |
| P0 | Implement Evaluations API | B |
| P0 | Implement Dashboard APIs (all 6 role dashboards) | B |
| P0 | Install axios + TanStack Query in frontend | A |
| P0 | Replace frontend demo auth with real JWT auth | A |
| P0 | Wire Employee Dashboard to API | A |
| P1 | Wire all CRUD pages to APIs (Projects, Tasks, Users) | A |
| P1 | Wire Submit Progress + file upload | A |
| P1 | Wire Task Review + Evaluation | A |
| P1 | Implement form validations with react-hook-form + zod | A |

### Phase 3: Advanced Features (Weeks 9-12) — AI + Real-time + Reporting

| Priority | Tasks | Part |
|---|---|---|
| P0 | Implement AI Integration Service (all 5 agents) | D |
| P1 | Implement SignalR notification hub | B |
| P1 | Wire real-time notifications in frontend | A |
| P1 | Implement Report generation + PDF | B |
| P1 | Wire Reports page + export functionality | A |
| P1 | Implement Communication API | B |
| P1 | Wire Communication Center | A |
| P2 | Implement audit log export (CSV) | B |
| P2 | Wire Role Matrix to API (CEO saves) | A |
| P2 | Wire AI Control Panel to API | A |

### Phase 4: Polish & Production (Weeks 13-16) — Testing + DevOps + Launch

| Priority | Tasks | Part |
|---|---|---|
| P0 | Write backend unit tests (80% coverage target) | G |
| P0 | Write backend integration tests | G |
| P1 | Set up CI/CD pipeline (GitHub Actions) | F |
| P1 | Docker Compose for local dev | F |
| P1 | Frontend component + page tests | G |
| P2 | E2E tests with Playwright | G |
| P2 | Performance/load testing | G |
| P2 | Security audit | G |
| P2 | Production deployment (Azure) | F |
| P2 | Monitoring + alerting setup | F |
| P3 | Finalize promotional video | H |
| P3 | Add i18n (English + Arabic) | A |

---

## Appendix: Technology Stack Summary

| Layer | Technology | Version |
|---|---|---|
| **Frontend** | React | 19.x |
| **Frontend Build** | Vite | 6.x |
| **Frontend CSS** | Tailwind CSS | 4.x |
| **Frontend Charts** | Recharts | 3.x |
| **Frontend Icons** | Lucide React | 0.57x |
| **Frontend State** | TanStack Query + Zustand | 5.x / 5.x |
| **Frontend Forms** | React Hook Form + Zod | 7.x / 3.x |
| **Frontend i18n** | React i18next | 15.x |
| **Backend Framework** | ASP.NET Core | 8.0 |
| **Backend ORM** | Entity Framework Core | 8.0 |
| **Backend Auth** | JWT Bearer + BCrypt | 8.x / 4.x |
| **Backend Validation** | FluentValidation | 11.x |
| **Backend Mapping** | AutoMapper | 12.x |
| **Backend Logging** | Serilog | 8.x |
| **Backend PDF** | QuestPDF | 2024.x |
| **Backend CSV** | CsvHelper | 33.x |
| **Backend Real-time** | SignalR | Built-in |
| **Backend Resilience** | Polly | 8.x |
| **Backend Jobs** | Hangfire | 1.8.x |
| **Database** | SQL Server | 2022 |
| **Cache** | Redis | 7.x |
| **AI Provider** | Azure OpenAI | Latest |
| **AI SDK** | Semantic Kernel | 1.x |
| **File Storage** | Azure Blob Storage | 12.x |
| **CI/CD** | GitHub Actions | — |
| **Containerization** | Docker + Docker Compose | — |
| **Testing (BE)** | xUnit + Moq + FluentAssertions | — |
| **Testing (FE)** | Vitest + React Testing Library | — |
| **Testing (E2E)** | Playwright | — |
| **Hosting (FE)** | Vercel or Azure Static Web Apps | — |
| **Hosting (BE)** | Azure App Service | — |
| **Monitoring** | Application Insights | — |
| **Video** | Remotion | — |

---

*End of Product Requirements Document*
