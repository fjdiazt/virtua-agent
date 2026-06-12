# Repository Guidelines

## Project Structure & Module Organization

This repository contains a local-first OpenAI-compatible orchestration API built with ASP.NET Core plus a separate React/Vite UI.

- `src/virtua-agent-api/` holds the ASP.NET Core API.
- `src/virtua-agent-api/Endpoints/` maps HTTP endpoints such as `/v1/chat/completions`, `/v1/models`, and orchestration routes.
- `src/virtua-agent-api/OpenAi/`, `virtua-agent/`, `Orchestration/`, `Tracing/`, and `Upstream/` contain DTOs, pipeline execution, SQLite trace storage, and upstream proxy logic.
- `src/virtua-agent-ui/` contains the React TypeScript Vite UI. It is separate from the .NET solution and builds static files into `src/virtua-agent-api/wwwroot/ui`.
- `src/virtua-agent-api/wwwroot/` stores API-served static assets.
- `tests/VirtuaAgent.Tests/` contains xUnit unit and integration-style tests.
- `docs/` contains design notes, plans, and drafts.

## Build, Test, and Development Commands

- `dotnet restore VirtuaAgent.slnx` restores NuGet packages.
- `dotnet build VirtuaAgent.slnx` compiles the API and test project.
- `dotnet test VirtuaAgent.slnx` runs the xUnit test suite.
- `dotnet run --project src/virtua-agent-api` starts the API locally.
- `npm install --prefix src/virtua-agent-ui` installs UI dependencies.
- `npm run dev --prefix src/virtua-agent-ui` starts the Vite UI at `http://localhost:5173`.
- `npm run build --prefix src/virtua-agent-ui` builds the UI into the API `wwwroot/ui` folder.

After starting the app, check `/swagger` for API exploration and `/ui/chat` for the built-in chat interface. Configure the upstream OpenAI-compatible server in `src/virtua-agent-api/appsettings.json` or `appsettings.Development.json`.

## Coding Style & Naming Conventions

Use C# with nullable reference types and implicit usings enabled. Follow the existing style: file-scoped namespaces in library/test files, four-space indentation, PascalCase for public types and members, camelCase for locals and parameters, and `_camelCase` for private fields. Keep endpoint handlers, orchestration logic, and trace storage in their existing module folders. Use React function components, TypeScript types, and Mantine components for UI work.

## Testing Guidelines

Tests use xUnit with `Microsoft.AspNetCore.Mvc.Testing`, `RichardSzalay.MockHttp`, and `coverlet.collector`. Name test classes after the behavior or component under test, for example `PipelineExecutorTests` or `ChatCompletionsEndpointTests`. Use descriptive `[Fact]` method names such as `StageInstructionsOverrideAutomaticRevisionInstruction`. Add tests beside related coverage in `tests/VirtuaAgent.Tests/`, and run `dotnet test VirtuaAgent.slnx` before submitting changes.

## Commit & Pull Request Guidelines

Recent history uses Conventional Commit-style subjects such as `feat: add pipeline tester` and `chore: initial commit`. Keep commit subjects short, imperative, and scoped to one change. Pull requests should include a concise summary, test results, linked issues when applicable, and screenshots or short notes for visible UI changes under `src/virtua-agent-ui`.

## Security & Configuration Tips

Do not commit local SQLite databases, logs, process IDs, or user-specific IDE files; `.gitignore` already excludes these. Avoid committing real upstream API keys or private endpoint URLs. Keep local-only configuration in development settings or environment variables.
