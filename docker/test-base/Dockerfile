# docker/test-base/Dockerfile
FROM ghcr.io/belay-dotnet/belay-build-base:latest AS test-base

# Install MicroPython unix port build dependencies
USER root
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    libffi-dev \
    pkg-config \
    libreadline-dev \
    libncurses5-dev \
    libncursesw5-dev \
    zlib1g-dev \
    libbz2-dev \
    liblzma-dev \
    libsqlite3-dev \
    libssl-dev \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

# Build MicroPython unix port using the exact submodule version from Belay.NET
# Copy the checked-out micropython submodule (preserves exact commit from .gitmodules)
WORKDIR /workspace
COPY micropython ./micropython

# Build production MicroPython from the exact submodule commit
# Note: submodules are already checked out in the copied directory
WORKDIR /workspace/micropython/ports/unix
RUN make \
    && make install

# Create alternative build with debugging enabled for development  
RUN make clean \
    && make DEBUG=1 \
    && cp build-standard/micropython /usr/local/bin/micropython-debug

# Extract MicroPython version information for runtime environment
RUN MICROPYTHON_VERSION=$(/usr/local/bin/micropython -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}-{sys.implementation.version}')" 2>/dev/null || echo "unknown") \
    && echo "export MICROPYTHON_VERSION=${MICROPYTHON_VERSION}" >> /etc/environment \
    && echo "MicroPython version: ${MICROPYTHON_VERSION}"

# Clean up build artifacts to reduce image size but preserve source for reference
RUN rm -rf /workspace/micropython/tests \
    && rm -rf /workspace/micropython/docs \
    && find /workspace/micropython -name "*.o" -delete \
    && find /workspace/micropython -name "*.a" -delete

# Install additional test tools and coverage utilities
RUN dotnet tool install -g coverlet.console
RUN dotnet tool install -g dotnet-reportgenerator-globaltool
RUN dotnet tool install -g dotnet-stryker

# Create test script for MicroPython subprocess validation
RUN echo '#!/bin/bash' > /usr/local/bin/test-micropython-subprocess && \
    echo 'set -e' >> /usr/local/bin/test-micropython-subprocess && \
    echo '' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "Testing MicroPython subprocess communication..."' >> /usr/local/bin/test-micropython-subprocess && \
    echo '' >> /usr/local/bin/test-micropython-subprocess && \
    echo '# Test basic execution' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "1. Basic execution test"' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "print('\''Hello from MicroPython'\'')" | micropython' >> /usr/local/bin/test-micropython-subprocess && \
    echo '' >> /usr/local/bin/test-micropython-subprocess && \
    echo '# Test raw REPL protocol simulation' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "2. Raw REPL protocol test"' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo -e "import sys\nsys.version" | micropython' >> /usr/local/bin/test-micropython-subprocess && \
    echo '' >> /usr/local/bin/test-micropython-subprocess && \
    echo '# Test error handling' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "3. Error handling test"' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "1/0" | micropython || echo "Error handling working"' >> /usr/local/bin/test-micropython-subprocess && \
    echo '' >> /usr/local/bin/test-micropython-subprocess && \
    echo '# Test module imports' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "4. Module import test"' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo -e "import os\nprint('\''os module available'\'')" | micropython' >> /usr/local/bin/test-micropython-subprocess && \
    echo '' >> /usr/local/bin/test-micropython-subprocess && \
    echo '# Test platform detection' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "5. Platform detection test"' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo -e "import sys\nprint('\''Platform:'\'', sys.platform)" | micropython' >> /usr/local/bin/test-micropython-subprocess && \
    echo '' >> /usr/local/bin/test-micropython-subprocess && \
    echo 'echo "MicroPython subprocess tests completed successfully"' >> /usr/local/bin/test-micropython-subprocess && \
    chmod +x /usr/local/bin/test-micropython-subprocess

USER builder
WORKDIR /workspace

# Environment variables for MicroPython testing
ENV MICROPYTHON_EXECUTABLE=/usr/local/bin/micropython
ENV MICROPYTHON_DEBUG_EXECUTABLE=/usr/local/bin/micropython-debug
# MICROPYTHON_VERSION is set dynamically during build based on submodule version

# Verify MicroPython installation and test subprocess communication
RUN micropython --version && \
    micropython-debug --version && \
    test-micropython-subprocess && \
    echo "MicroPython unix port ready for Belay.NET testing"

# Health check for MicroPython functionality
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD micropython -c "print('MicroPython health check OK')" || exit 1