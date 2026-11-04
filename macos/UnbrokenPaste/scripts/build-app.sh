#!/bin/bash
# Build "Unbroken Paste.app" (menu bar only, ad-hoc signed) into ./build
set -euo pipefail
cd "$(dirname "$0")/.."

APP="build/Unbroken Paste.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Universal app: Apple Silicon and Intel. Built per architecture so Command Line Tools alone are enough.
for ARCH in arm64 x86_64; do
    swift build -c release --arch "$ARCH"
done
lipo -create -output "$APP/Contents/MacOS/UP" \
    "$(swift build -c release --arch arm64 --show-bin-path)/UnbrokenPaste" \
    "$(swift build -c release --arch x86_64 --show-bin-path)/UnbrokenPaste"
cp -R Resources/ "$APP/Contents/Resources/"

# ClipBridge.Helper (C# Core) for both architectures, next to the app executable (#36).
# Without the .NET SDK the app still runs; every paste then passes through unchanged.
if command -v dotnet >/dev/null 2>&1; then
    for ARCH in arm64 x64; do
        HELPER_DIR="build/helper-$ARCH"
        dotnet publish ../../src/ClipBridge.Helper -c Release -r "osx-$ARCH" -o "$HELPER_DIR" --nologo -v quiet
        cp "$HELPER_DIR/ClipBridge.Helper" "$APP/Contents/MacOS/UnbrokenPaste.Helper-$ARCH"
        codesign --force --sign - "$APP/Contents/MacOS/UnbrokenPaste.Helper-$ARCH"
    done
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
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>0.1.1</string>
    <key>CFBundleVersion</key><string>2</string>
    <key>NSHumanReadableCopyright</key><string>© 2026 UP 팀 (장세찬 · 박재현 · 이창민 · 윤승희). GPLv3 라이선스.</string>
    <key>LSMinimumSystemVersion</key><string>13.0</string>
    <key>LSUIElement</key><true/>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

# iCloud-style synced folders keep adding com.apple.FinderInfo to the bundle, which codesign refuses.
# Sign a clean copy outside the project, then bring it back: the signature does not cover that attribute.
SIGN_DIR="$(mktemp -d)"
trap 'rm -rf "$SIGN_DIR"' EXIT
SIGNED="$SIGN_DIR/$(basename "$APP")"
ditto --norsrc --noextattr --noqtn "$APP" "$SIGNED"
codesign --force --sign - "$SIGNED"
codesign --verify --strict "$SIGNED"
rm -rf "$APP"
ditto --noqtn "$SIGNED" "$APP"
codesign --verify "$APP"
echo "Built $APP"
