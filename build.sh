#!/bin/bash
# Build, package, and deploy Supervertaler for Trados.
# Trados Studio must be CLOSED before running this script.
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/src/Supervertaler.Trados"
DIST_DIR="$SCRIPT_DIR/dist"
BUILD_DIR="$PROJECT_DIR/bin/Release"
DOTNET="${HOME}/.dotnet/dotnet"

PACKAGES_DIR="$APPDATA/Trados/Trados Studio/18/Plugins/Packages"
UNPACKED_DIR="$APPDATA/Trados/Trados Studio/18/Plugins/Unpacked/Supervertaler for Trados"
OLD_UNPACKED_DIR="$APPDATA/Trados/Trados Studio/18/Plugins/Unpacked/TermLens"

# Also clean up old Local install location if present
OLD_LOCAL_PACKAGES="$LOCALAPPDATA/Trados/Trados Studio/18/Plugins/Packages"
OLD_LOCAL_UNPACKED="$LOCALAPPDATA/Trados/Trados Studio/18/Plugins/Unpacked/Supervertaler for Trados"

echo "=== Building Supervertaler for Trados ==="
"$DOTNET" build "$PROJECT_DIR/Supervertaler.Trados.csproj" -c Release

# Ensure ARM64 native SQLite binary is in the build output.
# NuGet restore downloads it but MSBuild only copies x64/x86/arm to the output.
# Needed for Windows on ARM (Parallels on Apple Silicon, Surface Pro X, etc.).
ARM64_SRC="$USERPROFILE/.nuget/packages/sqlitepclraw.lib.e_sqlite3/2.1.6/runtimes/win-arm64/native/e_sqlite3.dll"
ARM64_DST="$BUILD_DIR/runtimes/win-arm64/native"
if [ -f "$ARM64_SRC" ] && [ ! -f "$ARM64_DST/e_sqlite3.dll" ]; then
    echo "  Copying win-arm64 native e_sqlite3.dll..."
    mkdir -p "$ARM64_DST"
    cp "$ARM64_SRC" "$ARM64_DST/e_sqlite3.dll"
fi

echo ""
PLUGIN_FILENAME="Supervertaler for Trados.sdlplugin"
echo "=== Packaging $PLUGIN_FILENAME (OPC format) ==="
mkdir -p "$DIST_DIR"
rm -f "$DIST_DIR/$PLUGIN_FILENAME"
python "$SCRIPT_DIR/package_plugin.py" "$BUILD_DIR" "$DIST_DIR/$PLUGIN_FILENAME"

echo ""
echo "=== Deploying to Trados Studio ==="

# Abort if Trados Studio is running — it locks plugin files and prevents
# a clean extraction on next start, leaving the plugin in a broken state.
if tasklist.exe 2>/dev/null | grep -qi "SDLTradosStudio\|TradosStudio"; then
    echo ""
    echo "  ERROR: Trados Studio is currently running."
    echo "  Close Trados Studio completely, then run this script again."
    echo ""
    echo "  (The plugin was built and packaged successfully — only the deploy step was skipped.)"
    exit 1
fi

# Wipe the Unpacked folder so Trados re-extracts cleanly on next start.
if [ -d "$UNPACKED_DIR" ]; then
    echo "  Removing stale Unpacked/Supervertaler for Trados..."
    rm -rf "$UNPACKED_DIR"
    echo "  Unpacked folder cleaned."
fi

# Clean up old TermLens unpacked directory if it exists
if [ -d "$OLD_UNPACKED_DIR" ]; then
    echo "  Removing old Unpacked/TermLens..."
    rm -rf "$OLD_UNPACKED_DIR"
    echo "  Old TermLens folder cleaned."
fi

# Clean up old Local install location (switched to Roaming)
if [ -f "$OLD_LOCAL_PACKAGES/Supervertaler.Trados.sdlplugin" ]; then
    echo "  Removing old Local Packages/Supervertaler.Trados.sdlplugin..."
    rm -f "$OLD_LOCAL_PACKAGES/Supervertaler.Trados.sdlplugin"
fi
if [ -d "$OLD_LOCAL_UNPACKED" ]; then
    echo "  Removing old Local Unpacked/Supervertaler.Trados..."
    rm -rf "$OLD_LOCAL_UNPACKED"
fi

# Remove old TermLens package if it exists
OLD_PACKAGE="$PACKAGES_DIR/TermLens.sdlplugin"
if [ -f "$OLD_PACKAGE" ]; then
    echo "  Removing old TermLens.sdlplugin..."
    rm -f "$OLD_PACKAGE"
fi

# Remove old dotted-name package (replaced by spaced name matching PlugInName in manifest)
OLD_DOTTED="$PACKAGES_DIR/Supervertaler.Trados.sdlplugin"
if [ -f "$OLD_DOTTED" ]; then
    echo "  Removing old Supervertaler.Trados.sdlplugin..."
    rm -f "$OLD_DOTTED"
fi
OLD_DOTTED_UNPACKED="$APPDATA/Trados/Trados Studio/18/Plugins/Unpacked/Supervertaler.Trados"
if [ -d "$OLD_DOTTED_UNPACKED" ]; then
    echo "  Removing old Unpacked/Supervertaler.Trados..."
    rm -rf "$OLD_DOTTED_UNPACKED"
fi

# Copy the new package.
mkdir -p "$PACKAGES_DIR"
cp "$DIST_DIR/$PLUGIN_FILENAME" "$PACKAGES_DIR/$PLUGIN_FILENAME"
echo "  Installed: $PACKAGES_DIR/$PLUGIN_FILENAME"

echo ""
echo "=== Done — start Trados Studio to load the updated plugin ==="
