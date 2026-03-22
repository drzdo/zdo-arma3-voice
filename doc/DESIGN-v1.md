# Arma 3 Voice Command & NPC Dialogue System — Design v1

## Overview

A voice-controlled AI companion system for Arma 3. The player speaks commands ("unit 1 and 2 move there") or talks to NPCs ("Miller, what's ahead?"). A local C# server handles speech recognition, intent parsing, LLM-powered NPC dialogue, and text-to-speech with spatial audio — all running on the same machine as the game.

## Architecture

```
┌─────────────────────────────────────────┐
│              Arma 3 Game                │
│                                         │
│  ┌───────────┐      ┌───────────────┐   │
│  │    SQF    │◄────►│  C# Extension │   │
│  │  Scripts  │      │  (NativeAOT)  │   │
│  └───────────┘      └───────┬───────┘   │
└─────────────────────────────┼───────────┘
                              │ TCP localhost:9500
┌─────────────────────────────┼───────────┐
│          C# Server          │           │
│                             │           │
│  ┌─────────┐  ┌──────────┐  ┌─────────┐ │
│  │   STT   │  │  Intent  │  │   TCP   │ │
│  │ Whisper │─►│  Parser  │  │  Bridge │ │
│  └─────────┘  └────┬─────┘  └─────────┘ │
│                    │                    │
│          ┌─────────┼─────────┐          │
│          ▼                   ▼          │
│  ┌──────────────┐   ┌──────────────┐    │
│  │ SQF Command  │   │ NPC Dialogue │    │
│  │  Generator   │   │   (LLM)     │    │
│  └──────────────┘   └──────┬───────┘    │
│                            ▼            │
│                     ┌──────────────┐    │
│  ┌──────────┐       │     TTS      │    │
│  │ Speakers │◄──────│ + Spatial /  │    │
│  │          │       │   Radio FX   │    │
│  └──────────┘       └──────────────┘    │
│                            ▲            │
│                     ┌──────┴───────┐    │
│                     │  Game State  │    │
│                     │  (per-frame) │    │
│                     └──────────────┘    │
└─────────────────────────────────────────┘
```

## Core Principle

The mod (extension + SQF) is a **dumb eval proxy**. It accepts SQF code strings from the server, evaluates them in-game, and returns results. All logic lives in the C# server. Once the mod is built, it never needs to change.

## Configuration (CBA Settings)

The mod uses CBA (Community Based Addons) settings framework for in-game configuration.

**CBA Settings:**

| Setting | Type | Default | Description |
|---|---|---|---|
| `zdo_arma_voice_serverHost` | STRING | `"127.0.0.1"` | C# server hostname |
| `zdo_arma_voice_serverPort` | SCALAR | `9500` | C# server port |
| `zdo_arma_voice_stateInterval` | SCALAR | `3` | Send state every N frames (1 = every frame) |

**Connection behavior:**

- On mission start, the extension attempts to connect to the configured host:port
- If the connection fails or drops, retry every 10 seconds
- Connection status shown via systemChat
- State push and RPC polling only run while connected

## Components

### 1. C# Extension (NativeAOT DLL)

Arma 3 loads native DLLs via `callExtension`. Using .NET 8+ with `PublishAot = true`, the extension compiles to a native `zdo_arma_voice_x64.dll` with no runtime dependency.

**NativeAOT note:** `System.Text.Json` requires source generators for AOT compatibility — use `[JsonSerializable]` attributes on a `JsonSerializerContext` instead of runtime reflection.

**Exported functions:**

| Export | Arma call | Purpose |
|---|---|---|
| `RVExtensionVersion` | automatic | Report version string |
| `RVExtension` | `"zdo_arma_voice" callExtension "poll"` | Simple commands: `poll`, `status` |
| `RVExtensionArgs` | `"zdo_arma_voice" callExtension ["respond", id, result]` | Parameterized: `state`, `respond`, `connect`, `ptt` |

**Internal design:**

- Background thread maintains persistent TCP connection to server
- Thread-safe queue for inbound RPC requests (server → game)
- `poll` drains one item from queue, returns it to SQF
- `respond` accepts RPC response from SQF, sends to server over TCP
- `state` accepts per-frame game state, sends to server over TCP
- `ptt` accepts PTT events with look target, sends to server over TCP

**callExtension buffer limit:** Arma 3 provides a ~10KB output buffer (`outputSize` param). RPC responses must fit within this. For large results, the SQF function should return only what's needed.

### 2. SQF Scripts

Thin glue packed into a `.pbo` addon. The entire SQF side is ~40 lines.

**CBA settings definition (`cba_settings.sqf`):**

```sqf
[
    "zdo_arma_voice_serverHost", "EDITBOX",
    "Server Host", "ZdoArmaVoice",
    "127.0.0.1", false
] call CBA_fnc_addSetting;

[
    "zdo_arma_voice_serverPort", "SLIDER",
    "Server Port", "ZdoArmaVoice",
    [1024, 65535, 9500, 0], false
] call CBA_fnc_addSetting;

[
    "zdo_arma_voice_stateInterval", "SLIDER",
    "State Push Interval (frames)", "ZdoArmaVoice",
    [1, 30, 3, 0], false
] call CBA_fnc_addSetting;
```

**PTT keybind (registered in init):**

```sqf
["ZdoArmaVoice", "zdo_arma_voice_ptt", "Push to Talk", {
    // Key down — capture look target, send PTT start
    private _lookPos = if (visibleMap) then {
        screenToWorld getMousePosition
    } else {
        screenToWorld [0.5, 0.5]
    };
    "zdo_arma_voice" callExtension ["ptt", "down", str _lookPos];
}, {
    // Key up — capture look target, send PTT stop
    private _lookPos = if (visibleMap) then {
        screenToWorld getMousePosition
    } else {
        screenToWorld [0.5, 0.5]
    };
    "zdo_arma_voice" callExtension ["ptt", "up", str _lookPos];
}] call CBA_fnc_addKeybind;
```

**Per-frame handler — state push + RPC poll:**

```sqf
zdo_arma_voice_frameCount = 0;

addMissionEventHandler ["EachFrame", {
    // Check connection
    if ("zdo_arma_voice" callExtension "status" == "0") exitWith {};

    // Throttled state push (every N frames, configurable via CBA)
    zdo_arma_voice_frameCount = zdo_arma_voice_frameCount + 1;
    if (zdo_arma_voice_frameCount >= zdo_arma_voice_stateInterval) then {
        zdo_arma_voice_frameCount = 0;

        private _pos = getPosASL player;
        private _dir = getDirVisual player;
        private _nearby = nearestObjects [player, ["Man"], 50] select {
            alive _x && _x != player
        };
        private _units = _nearby apply {
            [_x call BIS_fnc_netId, getPosASL _x]
        };
        "zdo_arma_voice" callExtension ["state", str [_pos, _dir, _units]];
    };

    // Poll for inbound RPC (every frame — must stay responsive)
    private _cmd = "zdo_arma_voice" callExtension "poll";
    if (_cmd != "") then {
        (parseSimpleArray _cmd) params ["_id", "_sqf"];
        private _result = "";
        _result = [_sqf] call {
            call compile (_this select 0)
        };
        "zdo_arma_voice" callExtension ["respond", _id, str _result];
    };
}];
```

**Reconnect loop (runs in parallel):**

```sqf
[] spawn {
    while {true} do {
        if ("zdo_arma_voice" callExtension "status" == "0") then {
            "zdo_arma_voice" callExtension [
                "connect",
                zdo_arma_voice_serverHost + ":" + str (round zdo_arma_voice_serverPort)
            ];
            systemChat "ZdoArmaVoice: connecting...";
        };
        sleep 10;
    };
};
```

### 3. C# Server

Console application. All intelligence lives here.

## Protocol

TCP, newline-delimited messages, on `localhost:9500`.

All messages from extension to server are SQF-formatted strings (produced by `str`). The extension prefixes each with a single-char type tag and a pipe separator before sending over TCP. The server parses accordingly.

### Message types (extension → server)

| Type tag | Meaning | Payload |
|---|---|---|
| `S` | State update | SQF array: `[[x,y,z],dir,[["netId",[x,y,z]], ...]]` |
| `R` | RPC response | `id\|result_string` |
| `P` | PTT event | `down\|[x,y,z]` or `up\|[x,y,z]` |

**Examples:**

```
S|[[3000,5000,10],45.2,[["2:3",[3005,5010,10]],["2:7",[3020,4990,10]]]]
R|10|"Sgt. Miller"
R|11|"WEST"
P|down|[3050,5020,0]
P|up|[3055,5022,0]
```

### Message types (server → extension)

| Type tag | Meaning | Payload |
|---|---|---|
| `C` | RPC call | `id\|sqf_code` |

**Examples:**

```
C|10|'2:3' call zdo_arma_voice_fnc_getUnitInfo
C|11|[['2:3','2:7'], [3050,5020,0]] call zdo_arma_voice_fnc_moveUnits
```

The extension queues inbound `C` messages. When SQF calls `poll`, it gets `[id, sqf_code]` as a parseable array.

### Format note

State and RPC result payloads use SQF `str` output, not JSON. For the types we use (numbers, strings, arrays), SQF `str` output is nearly identical to JSON — numeric arrays are identical, strings are double-quoted. The server parses this directly. No conversion in the extension.

## Function Registration

To avoid repeatedly compiling the same SQF code, the server registers reusable functions **once** right after connecting. It sends `compileFinal` calls that define global functions in the SQF namespace. After that, RPCs call these functions by name.

**On connect, server sends:**

```
C|0|zdo_arma_voice_fnc_getUnitInfo = compileFinal 'params ["_netId"]; private _unit = objectFromNetId _netId; str [name _unit, str side _unit, group _unit == group player, typeOf _unit, rankId _unit]'
C|0|zdo_arma_voice_fnc_moveUnits = compileFinal 'params ["_netIds", "_pos"]; { (objectFromNetId _x) doMove _pos } forEach _netIds; "ok"'
C|0|zdo_arma_voice_fnc_lookTarget = compileFinal 'if (visibleMap) then { str (screenToWorld getMousePosition) } else { str (screenToWorld [0.5, 0.5]) }'
```

**Then subsequent RPCs are short:**

```
C|10|'2:3' call zdo_arma_voice_fnc_getUnitInfo
C|11|[['2:3','2:7'], [3050,5020,0]] call zdo_arma_voice_fnc_moveUnits
```

`compileFinal` compiles once and prevents recompilation. The server re-registers on reconnect. Function definitions live in `SqfFunctions.cs` on the server side — the mod stays dumb.

**id 0** is reserved for fire-and-forget RPCs (registration, commands with no return value). The server does not wait for a response.

## Game State & Unit Registry

The server maintains a `UnitRegistry` — a dictionary of `netId → UnitInfo`.

```
Frame arrives with netId "2:3":
  → First seen?  → fire RPCs: '2:3' call zdo_arma_voice_fnc_getUnitInfo → cache result
  → Known?       → update position only
  → Missing for N frames? → evict from cache
```

Per-frame payload stays small. The server builds its world model lazily.

## Push-to-Talk & Look Target Capture

The player holds a CBA keybind to talk. SQF captures the look target (crosshair or map cursor) at both keydown and keyup, and sends it with the PTT event — no round-trip RPC needed.

```
PTT key down (in SQF)
  → captures look target locally (screenToWorld or map cursor)
  → sends P|down|[x,y,z] to server via extension
  → server starts recording mic

PTT key up (in SQF)
  → captures look target locally
  → sends P|up|[x,y,z] to server via extension
  → server stops recording mic
  → STT on recorded audio
  → intent parsing with look target context
```

The server uses the **keyup look target** as primary location ("where the player was looking when they finished speaking").

## Voice Command Pipeline

```
Player speaks (PTT held): "unit 1 and 2 move to that building"
  │
  ▼ PTT up arrives with look target [3050, 5020, 0]
  │
  ▼ STT (Whisper)
  "unit one and two move to that building"
  │
  ▼ Intent Parser (Gemini Flash 2.0)
  Receives: transcribed text + unit registry snapshot (names, roles, positions)
  Returns structured JSON:
    { "action": "move", "units": ["Miller", "Johnson"], "location": "look_target" }
  │
  ▼ Server resolves:
    - "Miller" → netId "2:3" (fuzzy match in UnitRegistry)
    - "Johnson" → netId "2:7"
    - "look_target" → [3050, 5020, 0]
  │
  ▼ Sends RPC:
  C|0|[['2:3','2:7'], [3050,5020,0]] call zdo_arma_voice_fnc_moveUnits
```

The LLM-based intent parser handles natural language flexibly — "Miller and the medic, move up to that building" works because the LLM sees the full unit registry as context.

## NPC Dialogue Pipeline

Only one NPC responds at a time. `DialogueManager` owns the full lifecycle: resolve target, LLM call, TTS, spatial playback. Concurrent requests are queued.

```
Player speaks: "Miller, what's the situation ahead?"
  │
  ▼ STT (Whisper)
  "miller what's the situation ahead"
  │
  ▼ Intent Parser (Gemini Flash 2.0)
  { "action": "dialogue", "target": "Miller", "text": "what's the situation ahead" }
  │
  ▼ DialogueManager picks it up (or queues if busy)
  │
  ▼ Resolve "Miller" → netId "2:3" (fuzzy match in UnitRegistry)
  │
  ▼ LLM call (Claude API) with context:
    - NPC name, role, unit class
    - Nearby units and their sides (from UnitRegistry)
    - Recent dialogue history (last few exchanges)
  │
  ▼ LLM response: "Command, I've got eyes on movement in the tree line.
     Two contacts, bearing north-east, maybe 200 meters out."
  │
  ▼ TTS → audio samples
  │
  ▼ During playback, SpatialMixer reads NPC position from live GameState
    (Miller is 8m away, 30° right of player → pan right, slight attenuation)
  │
  ▼ Output to speakers
```

## Audio System

All audio plays through the C# server directly to the player's speakers/headphones. Nothing is piped back through Arma 3.

### Spatial Audio (nearby NPC talking)

Given player position + direction and NPC position, compute:

- **Bearing**: angle from player direction to NPC → stereo pan (L/R)
- **Distance**: distance to NPC → volume attenuation (inverse distance, clamped)
- **Muffling**: distance → low-pass filter cutoff (farther = more muffled)

### Radio Audio (NPC on radio)

No spatial processing. Instead:

- Centered mono output
- Band-pass filter (300Hz – 3kHz) for radio frequency response
- Background static/noise mixed in
- Squelch sound at start and end of transmission

### Comparison

| Property | Spatial (nearby) | Radio |
|---|---|---|
| Stereo pan | Yes (positional) | No (centered) |
| Distance falloff | Yes | No |
| Low-pass / muffling | Distance-based | No |
| Band-pass filter | No | Yes (300Hz–3kHz) |
| Static / noise | No | Yes |
| Squelch | No | Yes (open/close) |

## Project Structure

```
arma3-mic/
├── ZdoArmaVoice.sln
├── src/
│   ├── ZdoArmaVoice.Extension/        # NativeAOT → zdo_arma_voice_x64.dll
│   │   ├── ZdoArmaVoice.Extension.csproj
│   │   ├── Exports.cs               # RVExtension* exports
│   │   ├── TcpClient.cs             # background TCP connection
│   │   └── CommandQueue.cs          # thread-safe inbound/outbound queues
│   │
│   └── ZdoArmaVoice.Server/           # Console app
│       ├── ZdoArmaVoice.Server.csproj
│       ├── Program.cs
│       ├── Net/
│       │   └── TcpBridge.cs         # TCP listener, protocol handling
│       ├── Game/
│       │   ├── GameState.cs          # latest player pos/dir, updated from state messages
│       │   ├── UnitRegistry.cs       # netId → cached unit info
│       │   ├── RpcClient.cs          # send SQF, await response by id
│       │   └── SqfFunctions.cs       # registered SQF function definitions (string constants)
│       ├── Speech/
│       │   ├── SpeechRecognizer.cs   # Whisper STT (mic → text)
│       │   └── SpeechSynthesizer.cs  # TTS (text → audio samples)
│       ├── Audio/
│       │   ├── AudioPlayer.cs        # NAudio output device
│       │   ├── SpatialMixer.cs       # pan, attenuation, low-pass
│       │   └── RadioEffect.cs        # band-pass, static, squelch
│       └── Ai/
│           ├── IntentParser.cs       # Gemini Flash — speech text → structured intent
│           ├── DialogueManager.cs    # owns NPC dialogue lifecycle, queues requests
│           ├── NpcDialogue.cs        # Claude API — context + prompt → NPC response
│           └── CommandExecutor.cs    # intent → SQF via RpcClient (action dispatch)
│
├── mod/                             # @zdo_arma_voice (Arma 3 mod folder)
│   └── addons/
│       └── zdo_arma_voice/
│           ├── config.cpp            # CfgPatches, CfgFunctions
│           ├── cba_settings.sqf      # CBA settings definitions (host, port, interval)
│           └── fn_init.sqf           # keybind, connect loop, EachFrame handler
│
└── doc/
    └── DESIGN-v1.md                 # this file
```

## Deployment

```
@zdo_arma_voice/                          # drop into Arma 3 directory, enable in launcher
├── zdo_arma_voice_x64.dll                # compiled extension (NativeAOT output)
└── addons/
    └── zdo_arma_voice.pbo                # packed SQF scripts
```

The C# server runs separately as a console app on the same machine.

## Technology Choices

| Component | Choice | Rationale |
|---|---|---|
| Extension | C# + NativeAOT (.NET 8) | Single language, shares code with server |
| STT | Whisper.net | Offline, good accuracy, C# bindings for whisper.cpp |
| TTS | Piper | Offline, fast, natural sounding |
| Intent parsing | Gemini Flash 2.0 | Fast, cheap, good enough for structured intent extraction |
| NPC dialogue | Claude API | Best quality for NPC personality and conversation |
| Audio I/O | NAudio | Mature C# audio library, DSP support |
| Mod config | CBA Settings | Standard Arma 3 config framework, in-game UI |

## Decisions

| Question | Decision |
|---|---|
| Intent parsing | LLM-based (Gemini Flash 2.0) — flexible natural language, no fixed grammar |
| Unit references | LLM resolves naturally — by name, role, index, or description. UnitRegistry provides context |
| Location references | Look target captured in SQF at PTT down/up. Map cursor if map is open, crosshair (screenToWorld) if in 3D. No round-trip RPC |
| Activation | Push-to-talk (CBA keybind). No always-listening |
| Multiple NPCs | One at a time. `DialogueManager` queues concurrent requests |
| State push frequency | Every N frames (CBA setting, default 3). RPC polling every frame |
| Protocol format | SQF `str` format over TCP (not JSON). Close enough to JSON for numbers/arrays, avoids conversion in extension |
| Fire-and-forget | id 0 = no response expected. Used for function registration and commands with no return value |

## Open Questions

- **NPC personality**: derive from unit class/role for MVP, or let the LLM improvise from name + role?
- **Conversation history**: keep last N exchanges per NPC? Global? For MVP, last 3-5 per NPC is probably fine.
- **Error feedback to player**: systemChat when STT/intent fails? Audio beep? TBD during implementation.
