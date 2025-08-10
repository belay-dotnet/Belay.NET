#\!/bin/bash

# Auto-install git hooks for new repository clones
# This script can be called from package.json, Makefile, or manually

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Check if we're in a git repository
if [ \! -d ".git" ]; then
    print_warning "Not in a git repository. Skipping git hooks setup."
    exit 0
fi

# Check if hooks are already configured
CURRENT_HOOKS_PATH=$(git config --get core.hooksPath 2>/dev/null || echo "")

if [ "$CURRENT_HOOKS_PATH" = ".githooks" ]; then
    print_info "Git hooks already configured correctly"
    exit 0
fi

print_info "Setting up Belay.NET development git hooks..."

# Configure git to use custom hooks directory
git config core.hooksPath .githooks
print_success "Git hooks path configured"

# Make sure all hooks are executable
if [ -d ".githooks" ]; then
    chmod +x .githooks/*
    print_success "Hook permissions set"
fi

# Make sure scripts directory is executable
if [ -d "scripts" ]; then
    chmod +x scripts/*.sh
    print_success "Script permissions set"
fi

print_success "Pre-commit documentation validation is now active\!"
echo ""
print_info "This will automatically validate documentation changes before commits"
print_info "Run './scripts/validate-docs.sh' for manual validation anytime"
EOF < /dev/null
