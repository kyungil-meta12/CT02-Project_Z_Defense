# Agent Instructions

- When reading files that may contain Korean text from PowerShell, force UTF-8 output and file decoding so comments and strings are not misread:
  ```powershell
  [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
  Get-Content -LiteralPath "path\to\file" -Encoding UTF8
  ```

## Required Project Context

- Before starting project work, read the following documents and apply their rules to the current task:

1. `Assets/__PROJECT__/TeamCodingConvention.cs`
   - Code style, Unity conventions, MemoryPool/PoolObject rules, and performance/GC guidelines.

2. `Assets/__PROJECT__/PROJECT_README.cs`
   - Game concept, system architecture, role boundaries, shared APIs, and current implementation status.

3. `Assets/__PROJECT__/TURRET_DATA_STRUCTURE_PLAN.cs`
   - Turret ScriptableObject structure, evolution/level model, placement flow, combat flow, and pooling rules.

## High Priority Rules

- Follow `TeamCodingConvention.cs` for all code changes.
- When writing a new method, add a brief Korean comment explaining its purpose.
- Write debugging logs in Korean.
- For turret, combat, projectile, placement, and damage popup work, consult `TURRET_DATA_STRUCTURE_PLAN.cs` first.
- When design intent or responsibility boundaries are unclear, use `PROJECT_README.cs` as the source of truth.
- Avoid direct modification of Private Assets originals when possible; prefer project-level wrappers, profiles, adapters, or duplicated prefabs under the project folder.
