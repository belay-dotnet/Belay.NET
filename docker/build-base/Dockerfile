# docker/build-base/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build-base

# Metadata
LABEL org.opencontainers.image.source="https://github.com/belay-dotnet/Belay.NET"
LABEL org.opencontainers.image.description="Belay.NET Build Base Image"
LABEL org.opencontainers.image.version="1.0.0"

# Test basic commands work
RUN dotnet --version

# Install system dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    gcc \
    make \
    git \
    curl \
    ca-certificates \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

# Test commands still work after package install
RUN dotnet --version

# Set up non-root user for security
RUN useradd -m -s /bin/bash builder

# Test as root still works
RUN dotnet --version

# Test as builder user
USER builder
WORKDIR /home/builder
RUN dotnet --version

# Set up NuGet cache directory
RUN mkdir -p /home/builder/.nuget/packages

# Health check
HEALTHCHECK --interval=30s --timeout=3s \
    CMD dotnet --version || exit 1

# Default to bash for interactive use, but allow command override
CMD ["/bin/bash"]