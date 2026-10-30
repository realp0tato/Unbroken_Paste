#!/bin/bash
# Build "Unbroken Paste.app" (menu bar only, ad-hoc signed) into ./build
set -euo pipefail
cd "$(dirname "$0")/.."

swift build -c release
BIN="$(swift build -c release --show-bin-path)/UnbrokenPaste"
APP="build/Unbroken Paste.app"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/UP"
cp -R Resources/ "$APP/Contents/Resources/"

# ClipBridge.Helper (C# Core) for this Mac's architecture, next to the app executable (#36).
# Without the .NET SDK the app still runs; every paste then passes through unchanged.
case "$(uname -m)" in
    arm64) RID=osx-arm64; ARCH=arm64 ;;
    *) RID=osx-x64; ARCH=x64 ;;
esac
HELPER_DIR="build/helper-$ARCH"
if command -v dotnet >/dev/null 2>&1; then
    dotnet publish ../../src/ClipBridge.Helper -c Release -r "$RID" -o "$HELPER_DIR" --nologo -v quiet
    cp "$HELPER_DIR/ClipBridge.Helper" "$APP/Contents/MacOS/UnbrokenPaste.Helper-$ARCH"
    codesign --force --sign - "$APP/Contents/MacOS/UnbrokenPaste.Helper-$ARCH"
else
    echo "warning: dotnet not found; building UP without the Helper (pastes pass through)" >&2
fi

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key><string>app.unbrokenpaste.menubar</string>
    <key>CFBundleName</key><string>Unbroken Paste</string>
    <key>CFBundleDisplayName</key><string>Unbroken Paste</string>
    <key>CFBundleExecutable</key><string>UP</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>0.1.0</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSMinimumSystemVersion</key><string>13.0</string>
    <key>LSUIElement</key><true/>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

codesign --force --sign - "$APP"
echo "Built $APP"
