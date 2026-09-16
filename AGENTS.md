# AGENTS.md — Workspace Guidelines for BadgeForge

## Mandatory Knowledge Graph Protocol

To prevent context token waste and maintain shared project memory, all coding agents working on BadgeForge must follow these two core rules:

### 1. Query the Knowledge Graph Before Reading Source Files
- **Do not blindly read files or run sweeping greps** across directories to understand project structure or code flow.
- Always check if `graphify-out/graph.json` exists.
- Query the graph first:
  - `graphify explain "<SymbolOrClass>"`: Inspect node definitions, inward/outward callers, and community relationships.
  - `graphify path "<ConceptA>" "<ConceptB>"`: Trace dependency chains and execution paths.
  - Consult `graphify-out/GRAPH_REPORT.md` for architectural god nodes, clusters, and suggested questions.
- Use file reading tools (`view_file`) only for targeted line ranges once the relevant files and symbols have been pinpointed via the graph.

### 2. Update the Knowledge Graph Before Finishing Any Task
- Whenever files are added, modified, or refactored, the knowledge graph must be brought into sync before completing the task.
- Run the graph update pipeline using `graphify-out/.graphify_python`:
  - Re-extract AST and update `graphify-out/graph.json` and `graphify-out/GRAPH_REPORT.md`.
  - Re-export the interactive visualization with `graphify export html`.
- Do not conclude your work without ensuring the knowledge graph is up to date for subsequent agent sessions.

---

## Project Standards & Design System
- **Architecture:** Maintain strict separation between UI (`BadgeForge.App`), Hardware Abstraction (`BadgeForge.Core.Printing`), Templates (`BadgeForge.Core.Templates`), Rendering (`BadgeForge.Core.Rendering`), and Data (`BadgeForge.Core.Data`).
- **Hardware Assertions:** Probed printer capabilities (DPI, printable bounds) must be validated against `CardFormat` at runtime — fail loudly on mismatch rather than silently stretching.
- **Color Palette:** Restrained usage of Grey Olive (`#7A918D`), Muted Teal (`#93B1A7`), and Tea Green (`#C5EDAC`, `#DBFEB8`) against neutral light backgrounds.
