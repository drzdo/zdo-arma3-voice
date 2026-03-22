# Setup

## Requirements

- Arma 3 with CBA_A3
- .NET 8 SDK (for building)
- One of: Gemini API key, Claude API key (for intent parsing and NPC dialog)
- One of: Whisper model file, Deepgram API key, Google/Azure STT key (for speech recognition)
- Optional: Piper TTS server or ElevenLabs API key (for NPC voice)

## Install mod

Download the latest release or build from source.

Copy `@zdo_arma_voice` folder (containing `zdo_arma_voice_x64.dll` and `addons/`) into your Arma 3 directory. Enable in the launcher.

### Quick install via PowerShell

```powershell
powershell -ExecutionPolicy Bypass -File scripts/install-latest.ps1
```

## Configuration

```
cp config-example.yaml config.yaml
```

Edit `config.yaml` with your settings. See `config-example.yaml` for all options.

`config.yaml` is gitignored — your keys stay local.

The server validates config on startup and fails with clear errors if required fields are missing.

### STT (speech recognition)

Pluggable — set `stt.system`:

```yaml
stt:
  system: deepgram  # whisper | deepgram | windows | google | azure
  mic_device: -1    # -1 = default. Run server to see device list.
  mic_mode: wasapi  # wasapi (shared mode) or mme (legacy, works alongside OBS)
  deepgram:
    api_key: YOUR_KEY
    model: nova-2
    language: ru
    encoding: linear16
    sample_rate: 16000
  whisper:
    model_path: ggml-base.en.bin
    language: en
```

### TTS (NPC speech)

Pluggable — set `tts.system`:

```yaml
tts:
  system: piper  # piper | elevenlabs
  piper:
    url: http://localhost:5000
  elevenlabs:
    api_key: YOUR_KEY
    model_id: eleven_multilingual_v2
    voices:
      default: YOUR_VOICE_ID
```

### LLM

Two LLM slots — intent parsing and NPC dialog:

```yaml
llm:
  intent:
    system: gemini  # gemini | claude
    gemini:
      api_key: YOUR_KEY
      model: gemini-2.0-flash-lite
  dialog:
    system: claude  # gemini | claude | none
    claude:
      api_key: YOUR_KEY
      model: claude-sonnet-4-20250514
```

Set `dialog.system: none` to disable NPC voice responses.

### Audio

```yaml
audio:
  radio_pan: 0.0    # -1.0 = left ear, 0.0 = center, 1.0 = right ear
  ack_chance: 0.5   # probability unit acknowledges command via voice
  radio:
    low_cut_hz: 300
    high_cut_hz: 3000
    distortion: 2.0
    noise_level: 0.02
```

### Microphone issues with OBS

If OBS blocks your microphone, set `mic_mode: mme` in config. MME uses the Windows audio mixer and always allows multiple apps to share the mic.

## Build

```
dotnet build
```

Builds on macOS/Linux (IL only). NativeAOT publish requires Windows. GitHub Actions handles release builds.

## Run

```
dotnet run --project src/server -- --config config.yaml
```

The server prints available microphone devices on startup — use the index for `mic_device` config.

## Russian language setup

### STT

Use the multilingual Whisper model instead of English-only:

```yaml
whisper:
  model_path: ggml-base.bin  # not ggml-base.en.bin
```

Download from https://huggingface.co/ggerganov/whisper.cpp/tree/main

Or use Deepgram/Google/Azure with `language: ru` or `language: ru-RU`.

### TTS

Use a Russian Piper voice model. Download from https://github.com/rhasspy/piper/blob/master/VOICES.md — e.g. `ru_RU-irina-medium`.

```
piper --model ru_RU-irina-medium.onnx --listen --port 5000
```

### NPC personality

Edit `src/server-data/010_core/coreUnitPersonality.sqf` to change how NPCs speak (language, tone, accent).

## Release

```
git tag v0.1.0 && git push --tags
```

GitHub Actions builds and creates a release with mod + server artifacts.
