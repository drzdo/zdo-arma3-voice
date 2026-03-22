# Setup

## Requirements

- Arma 3 with CBA_A3
- API keys (see Recommended setup below)

## Recommended setup

- **STT**: Deepgram (`stt.system: deepgram`) — fast, accurate, streams in real-time
- **LLM (intent)**: Gemini 2.0 Flash (`llm.intent.system: gemini`, `model: gemini-2.0-flash`) — cheap, fast, good enough for command parsing
- **LLM (dialog)**: Gemini 2.0 Flash (`llm.dialog.system: gemini`, `model: gemini-2.0-flash`) — works well for NPC responses too

## Install

### Quick install (PowerShell)

```powershell
powershell -ExecutionPolicy Bypass -File scripts/install-latest.ps1
```

### Manual install

1. Download the latest release from GitHub (two zips: mod + server)
2. Copy `@zdo_arma_voice/` folder into your Arma 3 directory. Enable in launcher.
3. Extract the server zip anywhere (e.g. `C:\ZdoArmaVoice\`)

## Configuration

```
cp config-example.yaml config.yaml
```

Edit `config.yaml` with your API keys. See `config-example.yaml` for all options.

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
    system: gemini
    gemini:
      api_key: YOUR_KEY
      model: gemini-2.0-flash
  dialog:
    system: gemini
    gemini:
      api_key: YOUR_KEY
      model: gemini-2.0-flash
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

## Run

```
ZdoArmaVoice.Server.exe --config config.yaml
```

The server prints available microphone devices on startup — use the index for `mic_device` if needed.

## Microphone issues with OBS

If OBS blocks your microphone, set `mic_mode: mme` in config. MME uses the Windows audio mixer and always allows multiple apps to share the mic.

## Russian language setup

### STT

Use Deepgram with `language: ru`, or the multilingual Whisper model:

```yaml
whisper:
  model_path: ggml-base.bin  # not ggml-base.en.bin
```

Download from https://huggingface.co/ggerganov/whisper.cpp/tree/main

### TTS

Use a Russian Piper voice model. Download from https://github.com/rhasspy/piper/blob/master/VOICES.md — e.g. `ru_RU-irina-medium`.

```
piper --model ru_RU-irina-medium.onnx --listen --port 5000
```

### NPC personality

Edit `src/server-data/010_core/coreUnitPersonality.sqf` to change how NPCs speak (language, tone, accent).
