#!/usr/bin/env bash
set -euo pipefail
# Signing identities are names in the local macOS Keychain, never private key material.
if [[ "$(uname -s)" != "Darwin" ]]; then echo "Build requires macOS with Xcode." >&2; exit 2; fi
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
: "${LUMORA_CODESIGN_KEY:?Set LUMORA_CODESIGN_KEY to your installed Apple signing identity name.}"
: "${LUMORA_CODESIGN_PROFILE:?Set LUMORA_CODESIGN_PROFILE to your installed provisioning profile name or UUID.}"
for tool in dotnet xcrun cmake ninja; do command -v "$tool" >/dev/null || { echo "Missing prerequisite: $tool" >&2; exit 2; }; done
xcodebuild -version
cd "$ROOT"
dotnet workload restore Lumora.Maui/Lumora.Maui.csproj -p:LumoraTargetFrameworks=net10.0-ios
bash Native.Mobile/build-ios.sh ios-arm64
dotnet publish Lumora.Maui/Lumora.Maui.csproj -f net10.0-ios -c Release \
  -p:LumoraTargetFrameworks=net10.0-ios -p:RuntimeIdentifier=ios-arm64 \
  -p:ArchiveOnBuild=true -p:BuildIpa=true -p:CodesignKey="$LUMORA_CODESIGN_KEY" \
  -p:CodesignProvision="$LUMORA_CODESIGN_PROFILE" -p:NuGetAudit=false \
  -p:PublishDir="$ROOT/artifacts/iPhone/"
find "$ROOT/Lumora.Maui/bin/Release/net10.0-ios" "$ROOT/artifacts/iPhone" -type f -name '*.ipa' -print
echo "Install only on devices allowed by the selected profile, or distribute through the Apple channel for that profile."
