# Belay.NET Development Makefile

.PHONY: setup build test docs clean install-hooks validate-docs

# Default target
all: setup build test

# Development environment setup
setup: install-hooks
	@echo "Setting up Belay.NET development environment..."
	dotnet restore
	@echo "✓ Development environment ready!"

# Install git hooks for documentation validation
install-hooks:
	@./scripts/install-hooks.sh

# Build the project
build:
	dotnet build

# Run tests
test:
	dotnet test

# Build and validate documentation
docs:
	@echo "Building and validating documentation..."
	@./scripts/validate-docs.sh

# Validate documentation only
validate-docs:
	@./scripts/validate-docs.sh

# Clean build artifacts
clean:
	dotnet clean
	rm -rf docs/.vitepress/dist

# Help target
help:
	@echo "Belay.NET Development Commands:"
	@echo "  make setup           - Setup development environment (includes git hooks)"
	@echo "  make build           - Build the project"
	@echo "  make test            - Run tests"
	@echo "  make docs            - Build and validate documentation"
	@echo "  make validate-docs   - Validate documentation only"
	@echo "  make install-hooks   - Install git hooks for documentation validation"
	@echo "  make clean           - Clean build artifacts"
	@echo "  make help            - Show this help message"