#!/bin/bash

# Pre-commit hook for Belay.NET
# This script runs code formatting and static analysis checks

set -e

echo "🔍 Running pre-commit checks..."

# Check if dotnet is available
if ! command -v dotnet &> /dev/null; then
    echo "❌ dotnet CLI not found. Please install .NET SDK."
    exit 1
fi

# Format code
echo "📝 Formatting code..."
if ! dotnet format --severity info --verbosity minimal --include src/ tests/; then
    echo "❌ Code formatting failed. Please fix formatting issues."
    exit 1
fi

# Build and check for warnings
echo "🔧 Building solution..."
if ! dotnet build --no-restore -warnaserror --verbosity minimal; then
    echo "❌ Build failed with warnings/errors. Please fix before committing."
    exit 1
fi

# Run unit tests (fast ones only)
echo "🧪 Running unit tests..."
if ! dotnet test tests/Belay.Tests.Unit/ --no-build --verbosity minimal; then
    echo "❌ Unit tests failed. Please fix before committing."
    exit 1
fi

# Check for security vulnerabilities
echo "🔒 Checking for security vulnerabilities..."
if ! dotnet list package --vulnerable --include-transitive 2>&1 | grep -q "no vulnerable packages"; then
    echo "⚠️  Vulnerable packages detected. Please review:"
    dotnet list package --vulnerable --include-transitive
    echo "Consider updating packages or adding suppressions if safe."
    # Don't fail on vulnerabilities, just warn
fi

echo "✅ All pre-commit checks passed!"