#!/bin/bash

# Development helper script for Belay.NET
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to run dotnet commands in container
dotnet_run() {
    echo -e "${GREEN}Running: dotnet $*${NC}"
    docker-compose -f docker-compose.dev.yml run --rm belay-dev dotnet "$@"
}

# Function to run general commands in container
run_in_container() {
    echo -e "${GREEN}Running: $*${NC}"
    docker-compose -f docker-compose.dev.yml run --rm belay-dev "$@"
}

# Function to start interactive development container
dev_shell() {
    echo -e "${GREEN}Starting development container...${NC}"
    docker-compose -f docker-compose.dev.yml run --rm belay-dev bash
}

# Function to build the solution
build() {
    echo -e "${GREEN}Building solution...${NC}"
    dotnet_run build
}

# Function to run tests
test() {
    echo -e "${GREEN}Running tests...${NC}"
    dotnet_run test
}

# Function to run tests with direct path mapping (for integration tests)
test_direct() {
    echo -e "${GREEN}Running tests with direct path mapping...${NC}"
    docker run --rm -v $(pwd):$(pwd) -w $(pwd) mcr.microsoft.com/dotnet/sdk:8.0 dotnet test "$@"
}

# Function to create new projects
create_solution() {
    echo -e "${GREEN}Creating .NET solution...${NC}"
    dotnet_run new sln --name Belay.NET
}

create_projects() {
    echo -e "${GREEN}Creating .NET projects...${NC}"
    
    # Core libraries
    dotnet_run new classlib --name Belay.Core --output src/Belay.Core --framework net8.0
    dotnet_run new classlib --name Belay.Attributes --output src/Belay.Attributes --framework net8.0
    dotnet_run new classlib --name Belay.Proxy --output src/Belay.Proxy --framework net8.0
    dotnet_run new classlib --name Belay.Sync --output src/Belay.Sync --framework net8.0
    dotnet_run new classlib --name Belay.PackageManager --output src/Belay.PackageManager --framework net8.0
    dotnet_run new console --name Belay.CLI --output src/Belay.CLI --framework net8.0
    dotnet_run new classlib --name Belay.Extensions --output src/Belay.Extensions --framework net8.0
    
    # Test projects
    dotnet_run new nunit --name Belay.Tests.Unit --output tests/Belay.Tests.Unit --framework net8.0
    dotnet_run new nunit --name Belay.Tests.Integration --output tests/Belay.Tests.Integration --framework net8.0
    dotnet_run new nunit --name Belay.Tests.Subprocess --output tests/Belay.Tests.Subprocess --framework net8.0
    
    echo -e "${GREEN}Adding projects to solution...${NC}"
    dotnet_run sln add src/Belay.Core/Belay.Core.csproj
    dotnet_run sln add src/Belay.Attributes/Belay.Attributes.csproj
    dotnet_run sln add src/Belay.Proxy/Belay.Proxy.csproj
    dotnet_run sln add src/Belay.Sync/Belay.Sync.csproj
    dotnet_run sln add src/Belay.PackageManager/Belay.PackageManager.csproj
    dotnet_run sln add src/Belay.CLI/Belay.CLI.csproj
    dotnet_run sln add src/Belay.Extensions/Belay.Extensions.csproj
    dotnet_run sln add tests/Belay.Tests.Unit/Belay.Tests.Unit.csproj
    dotnet_run sln add tests/Belay.Tests.Integration/Belay.Tests.Integration.csproj
    dotnet_run sln add tests/Belay.Tests.Subprocess/Belay.Tests.Subprocess.csproj
}

# Function to build MicroPython unix port
build_micropython() {
    echo -e "${GREEN}Building MicroPython unix port...${NC}"
    if [ ! -d "micropython" ]; then
        echo -e "${YELLOW}MicroPython submodule not found. Please add it first with: git submodule add https://github.com/micropython/micropython.git${NC}"
        return 1
    fi
    
    run_in_container bash -c "cd micropython/ports/unix && make submodules && make"
}

# Main command dispatcher
case "$1" in
    "shell"|"dev")
        dev_shell
        ;;
    "build")
        build
        ;;
    "test")
        test
        ;;
    "test-direct")
        shift
        test_direct "$@"
        ;;
    "init")
        create_solution
        create_projects
        ;;
    "build-micropython")
        build_micropython
        ;;
    "dotnet")
        shift
        dotnet_run "$@"
        ;;
    "run")
        shift
        run_in_container "$@"
        ;;
    *)
        echo -e "${YELLOW}Belay.NET Development Helper${NC}"
        echo ""
        echo "Usage: $0 {shell|build|test|test-direct|init|build-micropython|dotnet|run}"
        echo ""
        echo "Commands:"
        echo "  shell               Start interactive development container"
        echo "  build               Build the solution"
        echo "  test                Run all tests"
        echo "  test-direct <args>  Run tests with direct path mapping (for integration tests)"
        echo "  init                Initialize solution and projects"
        echo "  build-micropython   Build MicroPython unix port for testing"
        echo "  dotnet <args>       Run dotnet command in container"
        echo "  run <cmd>           Run arbitrary command in container"
        echo ""
        echo "Examples:"
        echo "  $0 shell                          # Start development environment"
        echo "  $0 dotnet --version               # Check .NET version"
        echo "  $0 dotnet new classlib -n MyLib   # Create new class library"
        echo "  $0 run ls -la                     # List files in container"
        ;;
esac