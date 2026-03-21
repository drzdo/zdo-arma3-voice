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

Edit `config.yaml` with your settings:

```yaml
server:
  port: 9500

whisper:
  model_path: ggml-base.en.bin

piper:
  url: http://localhost:5000

gemini:
  api_key: YOUR_GEMINI_API_KEY

claude:
  api_key: YOUR_CLAUDE_API_KEY
```

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

## Install mod

Copy `@arma3_mic` folder (with the published `arma3_mic_x64.dll` at root) into your Arma 3 directory. Enable in launcher.

## Release

```
git tag v0.1.0 && git push --tags
```
