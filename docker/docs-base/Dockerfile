# docker/docs-base/Dockerfile
FROM node:18-slim AS docs-base

# Metadata
LABEL org.opencontainers.image.source="https://github.com/belay-dotnet/Belay.NET"
LABEL org.opencontainers.image.description="Belay.NET Documentation Base Image"

# Install system dependencies including .NET SDK
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    unzip \
    git \
    python3 \
    python3-pip \
    pandoc \
    ca-certificates \
    gpg \
    --no-install-recommends \
    # Install Microsoft signing key and repository
    && curl -sSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
    && echo "deb [arch=amd64,arm64,armhf signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/repos/microsoft-ubuntu-jammy-prod jammy main" > /etc/apt/sources.list.d/microsoft-prod.list \
    && apt-get update \
    && apt-get install -y dotnet-sdk-8.0 \
    && rm -rf /var/lib/apt/lists/*

# Set up non-root user for security
RUN useradd -m -s /bin/bash docsbuilder
USER docsbuilder
WORKDIR /home/docsbuilder

# Install DocFX globally
RUN mkdir -p /home/docsbuilder/.dotnet/tools
ENV PATH="/home/docsbuilder/.dotnet/tools:${PATH}"
ENV DOTNET_ROOT="/usr/lib/dotnet"

# Download and install DocFX
USER root
RUN curl -L https://github.com/dotnet/docfx/releases/download/v2.78.3/docfx-linux-x64-v2.78.3.zip -o docfx.zip \
    && unzip docfx.zip -d /usr/local/bin \
    && chmod +x /usr/local/bin/docfx \
    && rm docfx.zip

# Install global npm packages for documentation (as root)
RUN npm install -g \
    vitepress@latest \
    markdown-it \
    @types/node

USER docsbuilder

# Install Python documentation tools
USER root
RUN pip3 install --no-cache-dir --break-system-packages \
    markdown \
    pyyaml \
    jinja2

USER docsbuilder

# Verify installations
RUN docfx --version && \
    node --version && \
    npm --version && \
    pandoc --version && \
    git --version && \
    dotnet --version

WORKDIR /workspace