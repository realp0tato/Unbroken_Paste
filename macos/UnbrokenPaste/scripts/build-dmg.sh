#!/bin/bash
# Package "build/Unbroken Paste.app" into build/UnbrokenPaste-<version>.dmg (#40).
# The DMG file, its mounted volume and the app all show the slime icon (Resources/AppIcon.icns).
# Run ./scripts/build-app.sh first.
set -euo pipefail
cd "$(dirname "$0")/.."

APP="build/Unbroken Paste.app"
ICON="Resources/AppIcon.icns"
VOLNAME="Unbroken Paste"
[ -d "$APP" ] || { echo "error: $APP not found; run ./scripts/build-app.sh first" >&2; exit 1; }
command -v SetFile >/dev/null || { echo "error: SetFile not found; install Command Line Tools (xcode-select --install)" >&2; exit 1; }

VERSION="$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' "$APP/Contents/Info.plist")"
DMG="build/UnbrokenPaste-$VERSION.dmg"
STAGE="build/dmg-stage"
RW="build/dmg-rw.dmg"
MOUNT="$PWD/build/dmg-mount"

# Window contents: the app and a shortcut to Applications to drag it onto.
rm -rf "$STAGE" "$RW" "$DMG"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
cp "$ICON" "$STAGE/.VolumeIcon.icns"

# Writable image first: the volume icon flag can only be set on the mounted volume.
hdiutil create -quiet -srcfolder "$STAGE" -volname "$VOLNAME" -fs HFS+ -format UDRW "$RW"
mkdir -p "$MOUNT"
trap 'hdiutil detach -quiet "$MOUNT" 2>/dev/null || true' EXIT
hdiutil attach -quiet -nobrowse -noautoopen -mountpoint "$MOUNT" "$RW"
SetFile -a C "$MOUNT"  # use .VolumeIcon.icns as the volume icon
xattr -cr "$MOUNT/$(basename "$APP")"  # drop what a synced project folder added to the copy
codesign --verify --strict "$MOUNT/$(basename "$APP")"
hdiutil detach -quiet "$MOUNT"
trap - EXIT
hdiutil convert -quiet "$RW" -format UDZO -o "$DMG"
rm -rf "$STAGE" "$RW" "$MOUNT"

# Icon of the .dmg file itself in Finder.
osascript -l JavaScript - "$PWD/$ICON" "$PWD/$DMG" <<'JS' >/dev/null
ObjC.import("AppKit");
function run(argv) {
    const image = $.NSImage.alloc.initWithContentsOfFile(argv[0]);
    return $.NSWorkspace.sharedWorkspace.setIconForFileOptions(image, argv[1], 0);
}
JS

echo "Built $DMG"
