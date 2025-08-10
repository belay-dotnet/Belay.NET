#!/bin/bash

# Setup script for Belay.NET git hooks
# Configures pre-commit hooks for documentation validation

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_info "Setting up Belay.NET development environment..."

# Configure git to use custom hooks directory
git config core.hooksPath .githooks

print_success "Git hooks configured"

# Make sure all hooks are executable
chmod +x .githooks/*

print_success "Hook permissions set"

# Make sure scripts directory is executable
chmod +x scripts/*.sh

print_success "Script permissions set"

print_info "Pre-commit hooks are now active!"
print_info "Documentation will be validated before each commit"

echo ""
echo "Available commands:"
echo "  ./scripts/validate-docs.sh  - Run full documentation validation"
echo "  git commit                  - Will trigger automatic pre-commit validation"
echo ""
print_success "Setup complete!"