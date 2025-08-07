#!/bin/bash

# Install git hooks for Belay.NET project

set -e

REPO_ROOT=$(git rev-parse --show-toplevel)
HOOKS_DIR="$REPO_ROOT/.git/hooks"
SCRIPTS_DIR="$REPO_ROOT/scripts"

echo "🔧 Installing git hooks..."

# Install pre-commit hook
if [ -f "$SCRIPTS_DIR/pre-commit.sh" ]; then
    echo "📝 Installing pre-commit hook..."
    cp "$SCRIPTS_DIR/pre-commit.sh" "$HOOKS_DIR/pre-commit"
    chmod +x "$HOOKS_DIR/pre-commit"
    echo "✅ Pre-commit hook installed"
else
    echo "❌ Pre-commit script not found at $SCRIPTS_DIR/pre-commit.sh"
    exit 1
fi

echo "🎉 Git hooks installed successfully!"
echo ""
echo "The pre-commit hook will now run automatically before each commit to:"
echo "  • Format code with 'dotnet format'"
echo "  • Build solution with warnings as errors"
echo "  • Run unit tests"
echo "  • Check for vulnerable packages"
echo ""
echo "To skip hooks for a specific commit, use: git commit --no-verify"