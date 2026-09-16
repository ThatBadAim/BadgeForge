# Knowledge Graph Workflow & Token Optimization Rule

All coding agents operating in this workspace must adhere to the following mandatory knowledge graph workflow.

## 1. Query the Knowledge Graph Before Reading Files

To avoid wasting context tokens on redundant file reads, exploratory greps, or full-file inspections:

- **Check for Existing Graph First:** Always check if `graphify-out/graph.json` or `graphify-out/GRAPH_REPORT.md` exists.
- **Query the Graph First:** When investigating architecture, dependencies, symbols, class hierarchies, or component interactions, **query the graph before opening source files**:
  - Use `graphify explain "<NodeName>"` to inspect a class, method, or file and its connections.
  - Use `graphify path "<Source>" "<Target>"` to trace relationships between concepts or modules.
  - Read `graphify-out/GRAPH_REPORT.md` for high-level module architecture, god nodes, and community boundaries.
- **Targeted Reading Only:** Only read source files with `view_file` or edit tools after using the graph to locate the exact file and line ranges requiring modifications. Do not read entire folders or multiple unfamiliar files sequentially when the graph already maps their relationships.

---

## 2. Update the Knowledge Graph Before Completing Any Task

To ensure future agents and turns always have an accurate, up-to-date map of the codebase:

- **Mandatory Pre-Completion Update:** Before declaring any task, feature implementation, refactoring, or bugfix complete, you **MUST update the knowledge graph**.
- **Update Workflow:**
  - Execute the graphify update/rebuild pipeline using the configured Python environment in `graphify-out/.graphify_python`:
    - Run AST and semantic extraction for changed/new files.
    - Re-generate `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`, and export `graphify export html`.
    - Verify graph health (`graphify-out/graph.json` is non-empty and consistent).
- **Never finish a task without refreshing the graph** if any code or specification files were added, deleted, or modified.
