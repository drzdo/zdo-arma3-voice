# Development

## Build

```
dotnet build
```

Builds on macOS/Linux (IL only). NativeAOT publish requires Windows.

## Run locally

```
dotnet run --project src/server -- --config config.yaml
```

## Release

```
git tag v0.1.0 && git push --tags
```

GitHub Actions builds and creates a release with mod + server artifacts.

The CI workflow:
1. Builds server (single exe, win-x64)
2. Generates HEMTT `project.toml` from the git tag version
3. Creates `addons/` junction to `src/arma-mod/`
4. Packs PBO via HEMTT
5. Publishes extension DLL (NativeAOT, win-x64)
6. Uploads `zdo_arma_voice_mod` and `zdo_arma_voice_server` artifacts
7. On tag push: creates GitHub Release with zipped artifacts
