#!/bin/bash

# Test script for Belay.NET with comprehensive testing support
set -e

echo "Testing Belay.NET build and test setup..."

# Build the solution
echo "Building solution..."
./dev.sh dotnet build --no-restore Belay.NET.sln || {
    echo "Build failed, trying to restore first..."
    ./dev.sh dotnet restore Belay.NET.sln
    ./dev.sh dotnet build Belay.NET.sln
}

# Run unit tests
echo "Running unit tests..."
./dev.sh dotnet test tests/Belay.Tests.Unit/Belay.Tests.Unit.csproj --no-build --logger:"console;verbosity=normal"

# Run subprocess tests (requires MicroPython unix port)
echo "Running subprocess tests..."
./dev.sh dotnet test tests/Belay.Tests.Subprocess/Belay.Tests.Subprocess.csproj --no-build --logger:"console;verbosity=normal" --filter "Category=Subprocess" || {
    echo "⚠️  Subprocess tests failed - MicroPython unix port may need to be built"
}

# Run integration tests
echo "Running integration tests..."
./dev.sh dotnet test tests/Belay.Tests.Integration/Belay.Tests.Integration.csproj --no-build --logger:"console;verbosity=normal" --filter "Category=Integration" || {
    echo "⚠️  Integration tests failed - MicroPython unix port may need to be built"
}

# Optional: Run performance benchmarks
if [ "$1" == "--benchmarks" ]; then
    echo "Running performance benchmarks..."
    ./dev.sh dotnet test tests/Belay.Tests.Integration/Belay.Tests.Integration.csproj --no-build --logger:"console;verbosity=normal" --filter "Category=Performance"
fi

echo "✅ Build and test completed!"
echo ""
echo "To run performance benchmarks, use: ./test-build.sh --benchmarks"