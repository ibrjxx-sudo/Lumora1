#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-ios-arm64}"
bash "$ROOT/Native.Mobile/build-ios.sh" "$RID"
dotnet restore "$ROOT/Lumora.Maui/Lumora.Maui.csproj" -p:LumoraTargetFrameworks=net10.0-ios -r "$RID"
if [[ "$RID" == ios-arm64 ]]; then
  : "${LUMORA_CODESIGN_KEY:?Set the Apple Distribution certificate name in this environment variable}"
  : "${LUMORA_PROVISION_PROFILE:?Set the App Store distribution provisioning profile name or UUID}"
  dotnet publish "$ROOT/Lumora.Maui/Lumora.Maui.csproj" -f net10.0-ios -c Release -r "$RID" \
    -p:LumoraTargetFrameworks=net10.0-ios -p:ArchiveOnBuild=true -p:BuildIpa=true \
    -p:CodesignKey="$LUMORA_CODESIGN_KEY" -p:CodesignProvision="$LUMORA_PROVISION_PROFILE" -p:Registrar=static
else
  dotnet build "$ROOT/Lumora.Maui/Lumora.Maui.csproj" -f net10.0-ios -c Release -r "$RID" \
    -p:LumoraTargetFrameworks=net10.0-ios -p:EnableCodeSigning=false -p:Registrar=static
fi
