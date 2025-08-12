#!/bin/bash
set -e

# Belay.NET Docker Development Helper Script
# Provides convenient commands for Docker-based development workflow

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Show usage
show_usage() {
    cat << EOF
Belay.NET Docker Development Helper

USAGE:
    $0 <command> [options]

COMMANDS:
    dev                 Start development environment
    build               Build project in container
    test                Run tests in container
    docs                Start documentation server
    micropython         Start interactive MicroPython session
    shell               Enter development shell
    clean               Clean up containers and volumes
    rebuild             Force rebuild all images
    ci                  Run full CI pipeline locally
    status              Show container status
    logs                Show container logs

EXAMPLES:
    $0 dev              # Start development environment
    $0 shell            # Enter development shell
    $0 test             # Run all tests
    $0 build Release    # Build in Release configuration
    $0 docs             # Start docs server on http://localhost:5173
    $0 clean            # Clean up everything

For more details, see docker-compose.yml profiles and documentation.
EOF
}

# Check if Docker and Docker Compose are available
check_dependencies() {
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed or not in PATH"
        exit 1
    fi

    if ! command -v docker-compose &> /dev/null; then
        print_error "Docker Compose is not installed or not in PATH"
        exit 1
    fi
}

# Wait for container to be ready
wait_for_container() {
    local container_name="$1"
    local max_attempts=30
    local attempt=1

    print_info "Waiting for container '$container_name' to be ready..."
    
    while [ $attempt -le $max_attempts ]; do
        if docker exec "$container_name" echo "Container ready" &> /dev/null; then
            print_success "Container '$container_name' is ready"
            return 0
        fi
        
        echo -n "."
        sleep 1
        ((attempt++))
    done
    
    print_error "Container '$container_name' failed to start within $max_attempts seconds"
    return 1
}

# Main command processing
case "${1:-help}" in
    "dev"|"development")
        print_info "Starting development environment..."
        docker-compose --profile dev up -d
        wait_for_container "belay-net-dev"
        print_success "Development environment ready!"
        print_info "Enter shell with: $0 shell"
        ;;
        
    "shell"|"bash")
        print_info "Entering development shell..."
        if ! docker exec -it belay-net-dev bash 2>/dev/null; then
            print_warning "Development container not running, starting..."
            docker-compose --profile dev up -d
            wait_for_container "belay-net-dev"
            docker exec -it belay-net-dev bash
        fi
        ;;
        
    "build")
        config="${2:-Debug}"
        print_info "Building project in $config configuration..."
        docker-compose --profile build run --rm build dotnet build --configuration "$config"
        print_success "Build completed successfully"
        ;;
        
    "test")
        print_info "Running tests in container..."
        docker-compose --profile test run --rm test dotnet test --logger console --verbosity normal
        print_success "Tests completed"
        ;;
        
    "docs")
        print_info "Starting documentation server..."
        docker-compose --profile docs up -d
        print_success "Documentation server started"
        print_info "Visit http://localhost:5173 for live documentation"
        print_info "Stop with: docker-compose --profile docs down"
        ;;
        
    "micropython"|"repl")
        print_info "Starting interactive MicroPython session..."
        docker-compose --profile micropython run --rm micropython
        ;;
        
    "clean")
        print_info "Cleaning up containers and volumes..."
        docker-compose down --volumes --remove-orphans
        docker-compose --profile all down --volumes --remove-orphans
        print_success "Cleanup completed"
        ;;
        
    "rebuild")
        print_info "Force rebuilding all images..."
        docker-compose down --volumes --remove-orphans
        docker-compose --profile all build --no-cache
        print_success "Rebuild completed"
        ;;
        
    "ci")
        print_info "Running full CI pipeline locally..."
        docker-compose --profile ci up --build --abort-on-container-exit
        if [ $? -eq 0 ]; then
            print_success "CI pipeline completed successfully"
        else
            print_error "CI pipeline failed"
            exit 1
        fi
        ;;
        
    "status")
        print_info "Container status:"
        docker-compose ps
        echo
        print_info "Images:"
        docker images | grep belay
        ;;
        
    "logs")
        container="${2:-belay-net-dev}"
        print_info "Showing logs for $container..."
        docker logs "$container" -f
        ;;
        
    "help"|"-h"|"--help")
        show_usage
        ;;
        
    *)
        print_error "Unknown command: $1"
        show_usage
        exit 1
        ;;
esac