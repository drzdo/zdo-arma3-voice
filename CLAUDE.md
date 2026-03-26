# ZdoArmaVoice

## What this is

Arma 3 voice command + NPC dialog mod. Player speaks via PTT, C# server handles STT (Whisper), intent parsing (Gemini Flash), NPC dialog (Claude API), TTS (Piper), and spatial audio (NAudio). The C# server is SQF-agnostic — all game logic, command definitions, and LLM prompt generation live in SQF data files.

Full design: `doc/DESIGN-v1.md`

## Architecture

- **Server** (`src/server/`) — C# console app. TCP listener, speech pipeline, LLM client, audio. All config from `config.yaml`. SQF-agnostic — doesn't know about specific commands.
- **Server Data** (`src/server-data/`) — SQF files defining core functions, shared helpers, and commands. Sent to game on connect.
- **Arma DLL** (`src/arma-dll/`) — NativeAOT C# DLL loaded by Arma 3 via `callExtension`. TCP client to server. Must compile on Windows (`win-x64`).
- **Arma Mod** (`src/arma-mod/`) — ~80 lines SQF. CBA settings, PTT keybind, EachFrame handler, reconnect loop. Symlinked from `addons/zdo_arma_voice/` for HEMTT.

## Build

```sh
dotnet build
```

Builds on macOS (IL only). NativeAOT publish (`dotnet publish`) requires Windows. GitHub Actions handles this.

## Key technical details

### Data files (`src/server-data/`)

All game logic lives in SQF files under `src/server-data/`. C# loads them on startup and sends them to the game on connect, in alphabetical order by path.

Structure:
- `src/server-data/000_reset.sqf` — resets registered commands
- `src/server-data/010_core/*.sqf` — core functions C# expects (coreRegisterCommand, coreCallCommand, coreIntentPrompt, coreGetCommandSchemas)
- `src/server-data/020_functions/*.sqf` — shared SQF helper functions
- `src/server-data/030_commands/*.sqf` — command definitions that self-register via `coreRegisterCommand`

### Command system

Commands are defined in SQF and self-register. Each command file:
1. Defines its function (receives `_args` hashmap + `_lookAtPosition`)
2. Calls `zdoArmaVoice_fnc_coreRegisterCommand` with: id, description, schema, function

The LLM returns `[{"command": "move", "args": {...}}]`. C# is agnostic to args — just proxies them to SQF via `coreCallCommand`.

Commands can return:
- Nothing (fire and forget)
- `{type: "dialog", targetNetId, systemInstructions, message}` — triggers dialog LLM + TTS
- `{ackSystemInstructions, ackMessage}` — triggers voice acknowledgment (subject to `ack_chance`)

### Intent prompt

`zdoArmaVoice_fnc_coreIntentPrompt` builds the full LLM prompt in SQF:
- Gathers game state (player info, squad, markers)
- Enumerates registered commands with descriptions and schemas
- Returns hashmap: `{systemInstructions, message, lookAtPosition}`

C# calls this, sends the result to the LLM, and parses the response. Mission context is hardcoded in this function — edit `src/server-data/010_core/coreIntentPrompt.sqf` to change it.

### SQF gotchas

- **Operator precedence**: All SQF binary operators evaluate **right-to-left** with equal precedence. This means `a distance b < 10` evaluates as `a distance (b < 10)` which is WRONG. Always parenthesize binary operator results before comparisons: `(a distance b) < 10`, `(a distance2D b) >= 5`, `("ext" callExtension "cmd") == "value"`. Rule of thumb: if a binary command's result is compared, wrap it in parens.
- **`sin`/`cos` take degrees**: SQF `sin` and `cos` take degrees, NOT radians. Do NOT convert with `* pi / 180`.
- **`callExtension` forms**:
  - Simple: `ext callExtension "fn"` → returns String.
  - Array: `ext callExtension ["fn", [arg1, arg2, ...]]` → returns `[result, returnCode, errorCode]`.
  - **CRITICAL**: The array form syntax is `["function", argumentsArray]` — the second element MUST be an array of arguments. `["fn", singleArg]` is WRONG, must be `["fn", [singleArg]]`.
  - All argument elements are auto-converted to strings by the engine.
- **`callExtension` export names (64-bit)**: Use plain names `RVExtension`, `RVExtensionArgs`, `RVExtensionVersion` — NO underscore prefix, NO `@N` stdcall decoration.
- **`callExtension` argument quoting**: SQF wraps string arguments in literal `"` quotes when passing to `RVExtensionArgs`. The C# extension strips these globally in `ReadArgs`.
- **`callExtension` provides ~10KB output buffer**. Keep RPC responses small.
- **Scheduled vs unscheduled**: `EachFrame` handler runs unscheduled (no `sleep`/`waitUntil`). The reconnect loop uses `spawn` (scheduled) for `sleep`.

### Protocol (TCP, localhost:9500)

Newline-delimited JSON messages with `"t"` (type) field. Uses `toJSON`/`fromJSON` for serialization.

### NativeAOT

- Extension uses `[UnmanagedCallersOnly]` for Arma 3 exports.
- `System.Text.Json` with NativeAOT needs source generators (`[JsonSerializable]`). Currently the extension avoids JSON — uses simple string protocol.

### Config

All server config in `config.yaml` (gitignored). Template in `config-example.yaml`.

**Rules:**
- Every time a new config field is added, it MUST also be added to `config-example.yaml`.
- No default values for API keys, voice IDs, model IDs — these must be set explicitly in config.
- Config validation runs on startup. Missing required fields cause a clear error and exit.

**STT/TTS are pluggable** via `stt.system` and `tts.system` fields:
- STT: `whisper` (local, Whisper.net) or `deepgram` (cloud API)
- TTS: `piper` (local HTTP server) or `elevenlabs` (cloud API)
- Each system has its own config block under `stt.*` / `tts.*`
- Interfaces: `ISpeechRecognizer`, `ISpeechSynthesizer` — add new impls by implementing these.

### Deployment

- Mod: `@zdo_arma_voice/zdo_arma_voice_x64.dll` + `@zdo_arma_voice/addons/zdo_arma_voice.pbo`
- Server: standalone console app, same machine

### CI/CD

- `.github/workflows/build.yml` — build + artifact upload on push/PR
- `.github/workflows/release.yml` — build + GitHub Release on tag push (`git tag v0.1.0 && git push --tags`)

### Self-review

After writing or modifying code, ALWAYS re-read the final version and check for:
- SQF operator precedence (parenthesize all binary operator results before comparisons)
- Variable shadowing in nested `forEach` loops
- Brace/bracket balance
- Non-ASCII characters in files that will run on Windows (PowerShell, etc.)
- File size for SQF files (must be under 10KB for callExtension buffer)
- Logic errors, unreachable code, off-by-one errors

Do not claim changes are done until this review is complete. If unsure about something, say so explicitly.

### Rules

- When a command is added, changed, or removed in `src/server-data/030_commands/`, update `README.md` to reflect the change.
- Files starting with `_` in `src/server-data/` are ignored by DataLoader and not loaded.
- Core functions (`010_core/`) must NOT call non-core functions (`020_functions/`). Files load in alphabetical order by path, so core loads before functions. If a core function needs a helper, move that helper to `010_core/` with a `core` prefix.
- Command descriptions (the string passed to `coreRegisterCommand`) must include trigger examples in both English and Russian, as both are primary usage languages. Example: `"Move units to a position. Triggers: go there, move forward, иди туда, двигайся вперёд."`
