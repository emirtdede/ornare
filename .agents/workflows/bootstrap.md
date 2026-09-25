---
description: Bootstrap Ornare's native solution and verify prerequisites without implementing feature creep.
---
# /bootstrap

1. Read `AGENTS.md`, all rules, project decisions and architecture/tooling docs.
2. Query Microsoft Learn MCP for the repository-pinned Windows App SDK baseline, current support/security status, WinUI packaging requirements and relevant Windows SDK. Do not upgrade a pin implicitly; record any proposed change for dependency review.
3. Verify installed .NET 10 SDK, Visual Studio/Build Tools C++/WinUI workloads, Windows SDK, Git and winapp CLI availability.
4. Validate pinned OSS package versions/licenses/security; update baseline docs only with evidence.
5. Create the solution and project files using normal `dotnet`/Visual Studio/MSBuild tooling and direct source editing. Do not generate product source via custom scripts.
6. Establish x64/ARM64 configurations, analyzers, nullable, warnings policy, central package management if appropriate.
7. Build clean empty/minimal projects.
8. Set up localization resource structure and package project.
9. Produce `production_artifacts/bootstrap/bootstrap-report.md` with commands/results/risks.
10. Do not build the full UI yet.
