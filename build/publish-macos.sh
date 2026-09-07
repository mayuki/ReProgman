#!/bin/bash
# Publishes a NativeAOT build of ReProgman and wraps it in a macOS application
# bundle. Requires the Xcode Command Line Tools for the native linker.
#
#   ./build/publish-macos.sh [runtime-identifier]
#
# Output: artifacts/publish-macos/ReProgman.app
#
# The bundle is not signed or notarized, so a copy that has been downloaded needs
#   xattr -d com.apple.quarantine /path/to/ReProgman.app
# before macOS will run it.
set -euo pipefail

BUNDLE_ID="org.misuzilla.reprogman"
VERSION="1.0.0"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ $# -ge 1 ]; then
    rid="$1"
elif [ "$(uname -m)" = "arm64" ]; then
    rid="osx-arm64"
else
    rid="osx-x64"
fi

output="$root/artifacts/publish-macos"
staging="$output/.publish-$rid"
bundle="$output/ReProgman.app"

echo "Publishing $rid with NativeAOT..."
rm -rf "$staging" "$bundle"
dotnet publish "$root/src/ReProgman/ReProgman.csproj" \
    --configuration Release \
    --runtime "$rid" \
    --output "$staging"

echo "Assembling $bundle..."
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
cp "$staging/ReProgman" "$bundle/Contents/MacOS/"
# Skia, HarfBuzz and the Avalonia backend stay next to the executable, where the
# default native library probing finds them.
cp "$staging"/*.dylib "$bundle/Contents/MacOS/"

cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDevelopmentRegion</key>
	<string>en</string>
	<key>CFBundleExecutable</key>
	<string>ReProgman</string>
	<key>CFBundleIdentifier</key>
	<string>$BUNDLE_ID</string>
	<key>CFBundleIconFile</key>
	<string>ReProgman</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<!-- Without this macOS resolves a bundle that ships no .lproj folders to the
	     development region, and the app would always come up in English. -->
	<key>CFBundleLocalizations</key>
	<array>
		<string>en</string>
		<string>ja</string>
	</array>
	<key>CFBundleName</key>
	<string>ReProgman</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>$VERSION</string>
	<key>CFBundleVersion</key>
	<string>$VERSION</string>
	<key>LSMinimumSystemVersion</key>
	<string>12.0</string>
	<!-- Retina displays render the aliased artwork as a crisp 2x enlargement.
	     Set this to false to get a soft 1x upscale instead. -->
	<key>NSHighResolutionCapable</key>
	<true/>
</dict>
</plist>
PLIST

# The Dock icon is the committed asset, which the app itself renders through
# build/export-icons.ps1, so it can never drift from the group icon shown inside
# the windows and this script needs neither a window server nor iconutil.
cp "$root/src/ReProgman/ReProgman.icns" "$bundle/Contents/Resources/ReProgman.icns"
echo "Copied Contents/Resources/ReProgman.icns"

rm -rf "$staging"

echo
echo "Done: $bundle"
echo "ReProgman.ini and Groups.ini are written next to the bundle; if that folder"
echo "is read only they move to ~/Library/Application Support/ReProgman."
