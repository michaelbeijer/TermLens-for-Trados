#!/bin/bash
# Build, package, and deploy Supervertaler for Trados.
# Trados Studio must be CLOSED before running this script.
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/src/Supervertaler.Trados"
DIST_DIR="$SCRIPT_DIR/dist"
BUILD_DIR="$PROJECT_DIR/bin/Release"
DOTNET="${HOME}/.dotnet/dotnet"

PACKAGES_DIR="$LOCALAPPDATA/Trados/Trados Studio/18/Plugins/Packages"
UNPACKED_DIR="$LOCALAPPDATA/Trados/Trados Studio/18/Plugins/Unpacked/Supervertaler.Trados"
OLD_UNPACKED_DIR="$LOCALAPPDATA/Trados/Trados Studio/18/Plugins/Unpacked/TermLens"

echo "=== Building Supervertaler for Trados ==="
"$DOTNET" build "$PROJECT_DIR/Supervertaler.Trados.csproj" -c Release

echo ""
echo "=== Packaging Supervertaler.Trados.sdlplugin (OPC format) ==="
mkdir -p "$DIST_DIR"
rm -f "$DIST_DIR/Supervertaler.Trados.sdlplugin"
python "$SCRIPT_DIR/package_plugin.py" "$BUILD_DIR" "$DIST_DIR/Supervertaler.Trados.sdlplugin"

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
    echo "  Removing stale Unpacked/Supervertaler.Trados..."
    rm -rf "$UNPACKED_DIR"
    echo "  Unpacked folder cleaned."
fi

# Clean up old TermLens unpacked directory if it exists
if [ -d "$OLD_UNPACKED_DIR" ]; then
    echo "  Removing old Unpacked/TermLens..."
    rm -rf "$OLD_UNPACKED_DIR"
    echo "  Old TermLens folder cleaned."
fi

# Remove old TermLens package if it exists
OLD_PACKAGE="$PACKAGES_DIR/TermLens.sdlplugin"
if [ -f "$OLD_PACKAGE" ]; then
    echo "  Removing old TermLens.sdlplugin..."
    rm -f "$OLD_PACKAGE"
fi

# Copy the new package.
mkdir -p "$PACKAGES_DIR"
cp "$DIST_DIR/Supervertaler.Trados.sdlplugin" "$PACKAGES_DIR/Supervertaler.Trados.sdlplugin"
echo "  Installed: $PACKAGES_DIR/Supervertaler.Trados.sdlplugin"

echo ""
echo "=== Done — start Trados Studio to load the updated plugin ==="
