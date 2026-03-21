---
name: unity-log
description: >
  Use this skill whenever the user wants to inspect Unity Editor logs, debug play mode errors,
  or understand what happened during the last Unity play session. Trigger phrases include:
  "check unity log", "read unity log", "show unity errors", "what happened in play mode",
  "unity console errors", "unity exceptions", "why did unity crash", "check the log",
  "ver el log de unity", "errores de unity", /unity-log.
  ALWAYS use this skill when the user asks about Unity runtime errors or play mode behavior —
  even if they don't say "log" explicitly.
version: 2.0.0
---

# unity-log

Reads `Logs/playmode.log` — a clean, session-only log file written by `PlayModeLogger.cs`.
The file is cleared at the start of every Play Mode session, so it always contains only the last run.

## Log location

```
C:/Users/VRINO/desarrollo/Conquest_prototype/Logs/playmode.log
```

Written by `Assets/Editor/PlayModeLogger.cs` via `Application.logMessageReceived`.

## Log format

```
=== Play mode started: 2026-03-21 10:30:00 ===
10:30:01.123 [INFO]  [HeroSpawnSystem] Hero spawned
10:30:01.456 [WARN]  [FormationSystem] No slot found for unit 3
10:30:02.789 [ERROR] NullReferenceException: Object reference not set
         Assets/Scripts/Hero/Systems/HeroSpawn.System.cs:98
         UnityEngine.Debug:Log (object)
10:30:05.000 [EXCEP] ArgumentException: Invalid argument
         ...
=== Play mode ended: 2026-03-21 10:30:10 ===
```

Prefixes: `[INFO]`, `[WARN]`, `[ERROR]`, `[EXCEP]`
Stack traces (errors/exceptions only) are indented with 9 spaces.

## Argument handling

| User says | Mode |
|-----------|------|
| `/unity-log` or no arg | `errors` — errors + exceptions only (default) |
| `/unity-log full` | `full` — all lines in the file |
| `/unity-log [BattleTestDebug]` | `tag` — only lines containing `[BattleTestDebug]` |
| `/unity-log tag=SomeTag` | `tag` — only lines containing `[SomeTag]` |

The default debug tag for Conquest battle tests is `[BattleTestDebug]`.

## How to execute

Use `ctx_execute(language="javascript", ...)`. Do NOT use Read or Bash cat directly.

### ERRORS mode (default)

```javascript
const fs = require('fs');
const LOG_PATH = 'C:/Users/VRINO/desarrollo/Conquest_prototype/Logs/playmode.log';

if (!fs.existsSync(LOG_PATH)) {
  console.log('No playmode.log found. Run Play Mode in Unity first.');
  process.exit(0);
}

const lines = fs.readFileSync(LOG_PATH, 'utf8').split('\n');
const errors = [], exceptions = [], warnings = [];
let i = 0;

while (i < lines.length) {
  const line = lines[i];
  if (line.includes('[ERROR]')) {
    const block = [line];
    while (i + 1 < lines.length && lines[i + 1].startsWith('         ')) block.push(lines[++i]);
    errors.push(block.join('\n'));
  } else if (line.includes('[EXCEP]')) {
    const block = [line];
    while (i + 1 < lines.length && lines[i + 1].startsWith('         ')) block.push(lines[++i]);
    exceptions.push(block.join('\n'));
  } else if (line.includes('[WARN]')) {
    warnings.push(line);
  }
  i++;
}

const lastLine = lines.filter(l => l.trim()).pop() || '';
const ended = lastLine.includes('Play mode ended');

console.log(`=== playmode.log — ${ended ? 'ENDED' : 'IN PROGRESS'} ===`);
console.log(`Errors: ${errors.length} | Exceptions: ${exceptions.length} | Warnings: ${warnings.length}\n`);

if (errors.length) {
  console.log('--- ERRORS ---');
  errors.forEach(e => { console.log(e); console.log('---'); });
}
if (exceptions.length) {
  console.log('--- EXCEPTIONS ---');
  exceptions.forEach(e => { console.log(e); console.log('---'); });
}
if (!errors.length && !exceptions.length) {
  console.log('No errors or exceptions in this Play Mode session.');
  if (warnings.length) {
    console.log('\n--- WARNINGS (first 10) ---');
    warnings.slice(0, 10).forEach(w => console.log(w));
  }
}
```

### TAG mode

When the user provides a tag (e.g. `/unity-log [BattleTestDebug]`), Claude interpolates the tag name into the script:

```javascript
const fs = require('fs');
const LOG_PATH = 'C:/Users/VRINO/desarrollo/Conquest_prototype/Logs/playmode.log';

if (!fs.existsSync(LOG_PATH)) {
  console.log('No playmode.log found. Run Play Mode in Unity first.');
  process.exit(0);
}

const lines = fs.readFileSync(LOG_PATH, 'utf8').split('\n');
const filterTag = 'BattleTestDebug'; // ← Claude replaces this with the actual tag
const taggedLines = lines.filter(l => l.includes(`[${filterTag}]`));

const lastLine = lines.filter(l => l.trim()).pop() || '';
const ended = lastLine.includes('Play mode ended');

console.log(`=== playmode.log — ${ended ? 'ENDED' : 'IN PROGRESS'} — filter: [${filterTag}] ===`);
console.log(`Total lines: ${lines.length} | Matching [${filterTag}]: ${taggedLines.length}\n`);

if (taggedLines.length === 0) {
  console.log(`No lines with tag [${filterTag}] in this session.`);
} else {
  taggedLines.forEach(l => console.log(l));
}
```

### FULL mode

```javascript
const fs = require('fs');
const LOG_PATH = 'C:/Users/VRINO/desarrollo/Conquest_prototype/Logs/playmode.log';

if (!fs.existsSync(LOG_PATH)) {
  console.log('No playmode.log found. Run Play Mode in Unity first.');
  process.exit(0);
}

const content = fs.readFileSync(LOG_PATH, 'utf8');
const lines = content.split('\n');
const lastLine = lines.filter(l => l.trim()).pop() || '';
const ended = lastLine.includes('Play mode ended');

console.log(`=== playmode.log FULL — ${ended ? 'ENDED' : 'IN PROGRESS'} | ${lines.length} lines ===\n`);
console.log(content);
```

## After running ctx_execute

Summarize for the user:
1. **Session status** — ended normally or still running?
2. **Counts** — errors / exceptions / warnings (or matching lines for tag mode)
3. **Full error text** — every error/exception block with its stack trace
4. **Your diagnosis** — brief root-cause analysis referencing project files where applicable
