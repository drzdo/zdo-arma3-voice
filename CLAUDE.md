# ArmaVoice

## What this is

Arma 3 voice command + NPC dialogue mod. Player speaks via PTT, C# server handles STT (Whisper), intent parsing (Gemini Flash), NPC dialogue (Claude API), TTS (Piper), and spatial audio (NAudio). The mod is a dumb eval proxy — all logic lives in the C# server.

Full design: `doc/DESIGN-v1.md`

## Architecture

- **ArmaVoice.Extension** (`src/ArmaVoice.Extension/`) — NativeAOT C# DLL loaded by Arma 3 via `callExtension`. TCP client to server. Must compile on Windows (`win-x64`).
- **ArmaVoice.Server** (`src/ArmaVoice.Server/`) — Console app. TCP listener, speech pipeline, AI, audio. All config from `config.yaml`.
- **Mod SQF** (`mod/addons/arma3_mic/`) — ~80 lines total. CBA settings, PTT keybind, EachFrame handler, reconnect loop.

## Build

```sh
dotnet build
```

Builds on macOS (IL only). NativeAOT publish (`dotnet publish`) requires Windows. GitHub Actions handles this.

## Key technical details

### SQF gotchas

- **Operator precedence**: All SQF binary operators evaluate **right-to-left**. Always parenthesize `callExtension` before comparing: `("ext" callExtension "cmd") == "value"`, NOT `"ext" callExtension "cmd" == "value"` (the latter compares first, then passes the bool to callExtension).
- **`callExtension` return types**: Simple form (`ext callExtension "fn"`) returns String. Array form (`ext callExtension ["fn", args...]`) returns Array `[result, returnCode, errorCode]`. Fire-and-forget calls can discard the array return.
- **`compileFinal`**: Compiles SQF once, result is immutable. Used to register server-pushed functions (`arma3_mic_fnc_*`). Re-register on reconnect.
- **`parseSimpleArray`**: Parses SQF array literals from strings. Used to parse poll responses from extension.
- **`str`** output for arrays/numbers is close enough to JSON for our protocol. No conversion needed in the extension.
- **Scheduled vs unscheduled**: `EachFrame` handler runs unscheduled (no `sleep`/`waitUntil`). The reconnect loop uses `spawn` (scheduled) for `sleep`.

### Protocol (TCP, localhost:9500)

Newline-delimited, type-tag prefix:
- `S|payload` — state (extension → server)
- `R|id|result` — RPC response (extension → server)
- `P|down/up|[x,y,z]` — PTT event (extension → server)
- `C|id|sqf_code` — RPC call (server → extension)

id=0 means fire-and-forget (no response expected).

### NativeAOT

- Extension uses `[UnmanagedCallersOnly]` for Arma 3 exports.
- `System.Text.Json` with NativeAOT needs source generators (`[JsonSerializable]`). Currently the extension avoids JSON — uses simple string protocol.
- `callExtension` provides ~10KB output buffer. Keep RPC responses small.

### Function registration

Server pushes `compileFinal` definitions on connect. SQF functions live in `SqfFunctions.cs` as string constants. RPCs then call short `'arg' call arma3_mic_fnc_name` instead of sending full SQF each time.

Registered functions: `getUnitInfo`, `moveUnits`, `attackTarget`, `holdPosition`, `regroup`, `setFormation`, `setStance`, `setSpeed`, `getTeamMembers`.

### Command system (4 dimensions)

Every voice command is parsed by the LLM into:
- **To whom**: "all", "team_red", "unit 2", name, role
- **What**: move, attack, hold, regroup, formation, dialogue
- **How**: stance (prone/crouch/standing) + speed (sprint/run/walk) — modifiers applied alongside any action
- **Where**: look_target (crosshair/map cursor), relative (100m_forward), cardinal (200m_north), explicit coords

Unit resolution supports: team colors (via SQF RPC), squad index, name fuzzy match, "all". Also supports Russian keywords.

### Config

All server config in `config.yaml` (gitignored). Template in `config.yaml.example`. Keys: server port, whisper model path, piper URL, Gemini API key, Claude API key.

### Deployment

- Mod: `@arma3_mic/arma3_mic_x64.dll` + `@arma3_mic/addons/arma3_mic.pbo`
- Server: standalone console app, same machine

### CI/CD

- `.github/workflows/build.yml` — build + artifact upload on push/PR
- `.github/workflows/release.yml` — build + GitHub Release on tag push (`git tag v0.1.0 && git push --tags`)
