# Production-Grade Unity Game Development Environment in Antigravity
**Architecture, Integration Guide, and Project Bootstrap Specification**

---

## 1. Executive Summary & System Architecture

This guide establishes the production-grade engineering foundation for developing commercial-quality Unity games using the **Antigravity Desktop Application**. 

### The Desktop-First Architecture
Antigravity operates as an autonomous agentic pair programmer. Unlike IDE-specific plugins that focus on code completion within an editor, Antigravity interacts across multiple operational layers:

```
┌────────────────────────────────────────────────────────┐
│             Antigravity Desktop Agent                  │
│   (Skills Engine • Multi-Step Task Automation)         │
└────────────────┬──────────────────────┬────────────────┘
                 │                      │
       MCP Stdio │                      │ CLI / Python Tooling
                 ▼                      ▼
┌─────────────────────────┐   ┌──────────────────────────┐
│   Node.js MCP Server    │   │  Project Automation Venv │
│  (unity-mcp / 52 tools) │   │ (.venv / unity_pipeline) │
└────────────────┬────────┘   └─────────┬────────────────┘
                 │ TCP :6400            │ Headless Batchmode
                 ▼                      ▼
┌────────────────────────────────────────────────────────┐
│                      Unity Editor                      │
│   (McpBridge.cs • ProjectValidator.cs • UPM Packages)  │
└────────────────────────────────────────────────────────┘
```

---

## 2. Agent Skills Inventory & Progressive Disclosure

Antigravity uses progressive disclosure: skill metadata is indexed globally, but full procedural context is loaded only when triggered by relevant tasks.

### Discovery Locations
- **Global Skills Directory**: `~/.gemini/antigravity/skills/<skill-name>/SKILL.md`
- **Project-Level Skills**: `<ProjectRoot>/.agents/skills/<skill-name>/SKILL.md`

### Installed Production Skill Packs (71 Skills Total)

1. **Official Unity Technologies Agent Plugin (32 Skills)**:
   - Core CLI & Package Management: `unity-cli`, `unity-package-management`
   - Rendering & Graphics: `urp-postprocessing`, `shader-graph`, `vfx-graph`, `lightmapping`
   - UI Architectures: `ui`, `ui-ugui`, `ui-uitk`
   - Audio & 2D Systems: `audio`, `2d-pixel-perfect`, `2d-animation`, `2d-physics`
   - AI & Navigation: `initialize-ai-navigation`
   - Profiling & Memory: `profiling-memory`, `profiling-cpu`

2. **Antigravity Unity & QA Skills (16 Skills)**:
   - Architecture & Coding: `unity-csharp-standards`, `unity-async-patterns`, `unity-addressables`
   - Auditing & Diagnostics: `unity-asset-audit`, `unity-verify`, `unity-mcp-operator-guide`
   - Automated QA: `unity-qa-generator`, `unity-playmode-tests`
   - Optimization: `unity-performance-audit`

3. **Unity Gameplay & Core Systems (8 Skills)**:
   - `unity-animation`, `unity-input-system`, `unity-physics`, `unity-scriptableobjects`, `unity-build-pipeline`

4. **Game Design & Engineering Disciplines (15 Skills)**:
   - `camera-systems`, `game-feel`, `game-ui-ux`, `performance-optimization`, `physics-tuning`, `save-systems`, `shader-programming`

---

## 3. Model Context Protocol (MCP) Bridge Configuration

The Unity MCP Bridge connects Antigravity directly to the live Unity Editor session via Stdio transport and local TCP loopback.

### Configuration Schema (`~/.gemini/config/mcp_config.json`)
```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": [
        "C:/Users/mpstme.student/Documents/Antigravity-Unity-Tools/unity-mcp/mcp-server/server.js"
      ],
      "env": {
        "UNITY_MCP_TOOLS": "core,assets,scenes,prefabs"
      }
    }
  }
}
```

### Context-Window Optimization (`UNITY_MCP_TOOLS`)
By default, exposing all 52 tools consumes ~8.8k tokens on every session turn. The environment is configured with tool groupings to preserve model context:
- `core` (19 tools, ~3.1k tokens): Scene inspection, object creation/manipulation, component management, console reader, play mode, screenshots.
- `core,assets,scenes,prefabs`: Provides complete asset and scene engineering capabilities without bloating context with unused terrain or blend tree schemas.

### Unity Editor Bridge (`McpBridge.cs`)
- Installed in: `Assets/Editor/UnityMcp/`
- Isolated in assembly definition: `CanAkbass.UnityMcp.Editor.asmdef`
- Listens on `127.0.0.1:6400`
- Safe domain-reload handling with undo history integration.

---

## 4. Production C# Engineering Standards

All code generated and maintained in this environment strictly adheres to enterprise C# game development principles:

### A. Lifecycle & MonoBehaviour Hygiene
- **Never call `GetComponent` in `Update()`**: Cache all component references in `Awake()` or serialize them in the Editor.
- **Null Safety**: Avoid explicit `== null` comparisons in tight loops where Unity's underlying C++ object lifetime check creates overhead. Use `ReferenceEquals` or guard clauses at entry points.
- **Domain Reload Resilience**: Static singletons and event subscriptions must register cleanup in `OnDisable()` / `OnDestroy()` or implement `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.

### B. Event-Driven Architecture & Decoupling
- Systems must communicate via C# `Action` events, Unity Events, or ScriptableObject channels.
- Avoid tight coupling between UI layers and business/game logic.

### C. Garbage Collection & Memory Management
- **Zero Allocation in Update**: Pre-allocate collections, string builders, and RaycastHit arrays (`Physics.RaycastNonAlloc`).
- **Object Pooling**: Mandatory for high-frequency spawning objects (projectiles, visual effects, floating damage text).

---

## 5. Scene & Asset Engineering Rules

1. **GUID and `.meta` File Preservation**:
   - Never move or delete an asset outside of the Unity Editor without corresponding `.meta` file operations.
   - Damaged `.meta` files break prefab linkages and serialized scene references.
2. **Prefab Hierarchies**:
   - Use Prefab Variants rather than duplicate prefabs for variations.
   - Core scene layouts must be composed of nested prefabs to facilitate multi-developer workflows and automated testing.
3. **Automated Validation (`ProjectValidator.cs`)**:
   - Integrated into `Assets/Editor/ProjectValidator.cs`.
   - Run interactive audit via **Tools > Engineering > Validate Project Assets & Scenes**.
   - Automatically detects missing scripts (`Missing Component`), broken prefab instances, and dangling serialized references.

---

## 6. Automated QA & Build Pipeline

A dedicated Python automation environment is established in `<ProjectRoot>/.venv`:

### Pipeline CLI (`Tools/unity_pipeline.py`)

1. **Verify MCP Connection**:
   ```powershell
   & .\.venv\Scripts\python.exe Tools/unity_pipeline.py mcp-status
   ```

2. **Execute Full Project Asset Validation**:
   ```powershell
   & .\.venv\Scripts\python.exe Tools/unity_pipeline.py validate
   ```
   *Generates structured JSON report in `Logs/validation_report.json`.*

3. **Run Automated Test Framework**:
   ```powershell
   & .\.venv\Scripts\python.exe Tools/unity_pipeline.py test --platform EditMode
   & .\.venv\Scripts\python.exe Tools/unity_pipeline.py test --platform PlayMode
   ```

---

## 7. Production Release Checklist (APK & Standalone)

Prior to exporting production builds:
1. [ ] **Zero Compilation Errors**: Ensure Unity console is free of compiler errors and assembly definitions resolve cleanly.
2. [ ] **Validation Pass**: Run `ProjectValidator.cs` to ensure 0 missing scripts and 0 dangling references.
3. [ ] **Build Settings Verification**: Confirm all target scenes are added and enabled in `EditorBuildSettings.scenes`.
4. [ ] **Target API & Keystore**: For Android/Quest APK builds, verify Minimum API Level (29+), Target API (34), and IL2CPP 64-bit architecture.
5. [ ] **Texture & Audio Compression**: Ensure ASTC texture compression is selected for mobile/VR targets.
