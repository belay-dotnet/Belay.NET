#!/bin/bash

# Docker-based lint script that matches CI environment exactly
# This script runs dotnet format in the same container used by CI

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CONTAINER_IMAGE="ghcr.io/belay-dotnet/belay-build-base:latest"
FALLBACK_IMAGE="mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim"

echo "🐳 Running lint checks in CI-equivalent Docker container..."
echo "📁 Repository: $REPO_ROOT"
echo "🏗️  Container: $CONTAINER_IMAGE"

# Check if Docker is available
if ! command -v docker &> /dev/null; then
    echo "❌ Docker not found. Please install Docker to use this script."
    exit 1
fi

# Pull the latest image to ensure we're using the same version as CI
echo "📥 Pulling latest container image..."
if ! docker pull "$CONTAINER_IMAGE" 2>/dev/null; then
    echo "⚠️  Failed to pull $CONTAINER_IMAGE"
    echo "   Falling back to public .NET SDK container: $FALLBACK_IMAGE"
    CONTAINER_IMAGE="$FALLBACK_IMAGE"
    docker pull "$CONTAINER_IMAGE"
fi

# Create a temporary directory for output with correct permissions
TMP_OUTPUT_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_OUTPUT_DIR"' EXIT

echo "🔍 Running code formatting verification..."

# Run the exact same command as CI
# CI runs with --user root, so we'll match that first for compatibility
# After success, we can add user ID 1000 option if needed
docker run --rm \
    --user root \
    --volume "$REPO_ROOT:/workspace:rw" \
    --env DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
    --env DOTNET_CLI_TELEMETRY_OPTOUT=true \
    --workdir /workspace \
    "$CONTAINER_IMAGE" \
    bash -c "
        set -e
        
        echo '📋 Environment Information:'
        echo '   .NET SDK Version:' \$(dotnet --version)
        echo '   User ID:' \$(id -u)
        echo '   Working Directory:' \$(pwd)
        echo '   Available files:' \$(ls -la | wc -l) files
        echo
        
        echo '📦 Restoring dependencies...'
        dotnet restore Belay.NET.sln
        
        echo
        echo '🎨 Running dotnet format (apply changes)...'
        dotnet format Belay.NET.sln --verbosity diagnostic
        
        echo
        echo '✅ Running formatting verification...'
        dotnet format Belay.NET.sln --verify-no-changes --verbosity diagnostic
        
        echo
        echo '🎉 Docker-based formatting completed successfully!'
        
        # Fix file ownership back to the original user if run as root
        if [ \$(id -u) -eq 0 ]; then
            echo '🔧 Fixing file ownership...'
            # Get the original owner from the .git directory (should be preserved)
            ORIG_UID=\$(stat -c '%u' /workspace/.git 2>/dev/null || echo 1000)
            ORIG_GID=\$(stat -c '%g' /workspace/.git 2>/dev/null || echo 1000)
            chown -R \$ORIG_UID:\$ORIG_GID /workspace 2>/dev/null || echo '   (ownership fix skipped)'
        fi
    "

echo
echo "✅ Docker-based lint completed successfully!"
echo "   Any formatting changes have been applied to match CI expectations."
echo "   Files in $REPO_ROOT have been updated if changes were needed."