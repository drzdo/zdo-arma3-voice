# ArmaVoice

Voice command and NPC dialogue system for Arma 3. Speak commands to control units and talk to NPCs with AI-generated responses and spatial audio.

See [doc/DESIGN-v1.md](doc/DESIGN-v1.md) for full design.

## Components

- **Extension** (`arma3_mic_x64.dll`) — NativeAOT C# DLL loaded by Arma 3, acts as TCP proxy
- **Server** (`ArmaVoice.Server`) — local C# app handling speech recognition, LLM intent parsing, NPC dialogue, and TTS with spatial audio
- **Mod** (`@arma3_mic`) — SQF scripts + CBA settings

## Requirements

- .NET 8 SDK
- Arma 3 with CBA_A3
- Whisper model file (`ggml-base.en.bin`)
- Piper TTS server (optional, for NPC speech)
- Gemini API key (intent parsing)
- Claude API key (NPC dialogue)

## Configuration

```
cp config.yaml.example config.yaml
```

Edit `config.yaml` with your settings. See `config.yaml.example` for all options.

STT and TTS are pluggable — set `system` to choose the backend:

```yaml
stt:
  system: whisper  # or: deepgram
  whisper:
    model_path: ggml-base.en.bin
  deepgram:
    api_key: ...
    model: nova-2
    language: en

tts:
  system: piper  # or: elevenlabs
  piper:
    url: http://localhost:5000
  elevenlabs:
    api_key: ...
    voice_id: ...
    model_id: eleven_multilingual_v2
```

The server validates config on startup and fails with clear errors if required fields are missing.

`config.yaml` is gitignored — your keys stay local.

## Build

```
dotnet build
```

## Run

```
dotnet run --project src/ArmaVoice.Server
```

Custom config path: `dotnet run --project src/ArmaVoice.Server -- --config /path/to/config.yaml`

## Russian language setup

### STT (speech recognition)

Use the multilingual Whisper model instead of English-only:

```yaml
whisper:
  model_path: ggml-base.bin   # not ggml-base.en.bin
```

Download it from https://huggingface.co/ggerganov/whisper.cpp/tree/main

### TTS (NPC speech)

Use a Russian Piper voice model. Download from https://github.com/rhasspy/piper/blob/master/VOICES.md — e.g. `ru_RU-irina-medium`.

Start Piper with the Russian model:

```
piper --model ru_RU-irina-medium.onnx --listen --port 5000
```

Config stays the same — Piper handles the language based on the loaded model:

```yaml
piper:
  url: http://localhost:5000
```

## Voice Commands

Hold the PTT key (default: Home) and speak. Works in any language — English, Russian, Ukrainian, etc.

### Movement

| Say | What happens |
|---|---|
| "Second, third — move to that building" | Units #2 and #3 move to your crosshair |
| "Второй, иди туда" | Unit #2 moves to your crosshair |
| "Everyone, move 100 meters north" | Whole squad moves 100m north of your position |
| "Red team, move to marker Alpha" | Red team moves to map marker "Alpha" |
| "Go south" | Last addressed units move 100m south |
| "Regroup!" / "Ко мне!" | Units return to the player |
| "Garrison that building" | Units enter and spread across building positions |

### Combat

| Say | What happens |
|---|---|
| "Attack that guy" / "Огонь по нему" | Squad engages the target |
| "Open fire" / "Weapons free" | Units set to fire at will |
| "Hold fire" / "Не стрелять" | Units cease fire |
| "Suppress that position" / "Подавить!" | Suppressive fire at crosshair |

### Stance & Speed

| Say | What happens |
|---|---|
| "Hit the dirt!" / "Ложись!" | Everyone goes prone |
| "Stand up" / "Встань" | Standing stance |
| "Crouch" / "Присядь" | Crouching stance |
| "Sprint!" / "Бегом!" | Full speed |
| "Walk" / "Шагом" | Slow speed |

### Behaviour

| Say | What happens |
|---|---|
| "Go stealth" / "Скрытно" | Stealth mode |
| "Stay alert" / "На чеку" | Aware mode |
| "Combat mode" / "К бою" | Combat mode |
| "Stand down" / "Вольно" | Safe mode |

### Stop & Hold

| Say | What happens |
|---|---|
| "Stop!" / "Freeze!" / "Стой!" | Cancel current action, stay responsive |
| "Hold position" / "Держать позицию" | Lock in place until new orders |

### Formation

| Say | What happens |
|---|---|
| "Wedge formation" | Switch to wedge |
| "Line" / "Column" / "Diamond" | Other formations |

### Vehicles

| Say | What happens |
|---|---|
| "Get in" / "В машину" | Units enter nearest vehicle at crosshair |
| "Get in as driver" / "Садись за руль" | Enter as driver |
| "Get out" / "Из машины" | Dismount current vehicle |

### Watch & Look

| Say | What happens |
|---|---|
| "Watch there" / "Смотри туда" | Units face the crosshair position |
| "Look south" / "Наблюдай на юг" | Units look 100m south |

### Reports (voice response via TTS)

| Say | What happens |
|---|---|
| "Report contacts" / "Кого видишь?" | Unit reports known hostiles with type, distance, bearing |
| "Where are you?" / "Где ты?" | Unit reports position relative to player |
| "Status report" / "Как дела?" | Unit reports health/wounds |

### ACE3 Medical (auto-detected)

| Say | What happens |
|---|---|
| "Heal yourself" / "Перевяжись" | Unit self-heals using ACE medical AI |
| "Medic!" / "Медик!" | Requests nearest medic |

### Dialogue (NPC conversation with TTS voice)

| Say | What happens |
|---|---|
| "Miller, what do you see?" | NPC responds in character via voice |
| "Петрович, что впереди?" | NPC responds in Russian |

### Map & Naming

| Say | What happens |
|---|---|
| "Mark this as Bravo" / "Отметь Альфа" | Creates map marker at crosshair |
| "This is vehicle Alpha" / "Это машина Альфа" | Names the object at crosshair for future reference |
| "Move to vehicle Alpha" | Uses previously named position |

### Targeting shortcuts

- **"there" / "that building" / "туда"** — uses crosshair / map cursor position
- **"100m forward" / "200m north"** — relative to player
- **"bearing 320, 200m"** — explicit azimuth
- **"marker Alpha"** — map marker (LLM fuzzy-matches the name)
- **"vehicle Alpha"** — previously named object

### Unit selection

- **"second" / "второй"** — unit #2 in squad
- **"Miller" / "Петрович"** — by name
- **"red team" / "группа 1"** — team color (1=red, 2=green, 3=blue, 4=yellow)
- **"everyone" / "все"** — whole squad
- No unit mentioned — reuses last addressed units

## Install mod

Copy `@arma3_mic` folder (with the published `arma3_mic_x64.dll` at root) into your Arma 3 directory. Enable in launcher.

## Release

```
git tag v0.1.0 && git push --tags
```
