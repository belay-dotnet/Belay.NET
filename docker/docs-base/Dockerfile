# docker/docs-base/Dockerfile
FROM node:18-slim AS docs-base

# Metadata
LABEL org.opencontainers.image.source="https://github.com/belay-dotnet/Belay.NET"
LABEL org.opencontainers.image.description="Belay.NET Documentation Base Image"

# Install system dependencies
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    unzip \
    python3 \
    python3-pip \
    pandoc \
    ca-certificates \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

# Set up non-root user for security
RUN useradd -m -s /bin/bash docsbuilder
USER docsbuilder
WORKDIR /home/docsbuilder

# Install DocFX globally
RUN mkdir -p /home/docsbuilder/.dotnet/tools
ENV PATH="/home/docsbuilder/.dotnet/tools:${PATH}"
ENV DOTNET_ROOT="/usr/share/dotnet"

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
RUN pip3 install --no-cache-dir \
    markdown \
    pyyaml \
    jinja2

USER docsbuilder

# Verify installations
RUN docfx --version && \
    node --version && \
    npm --version && \
    pandoc --version

WORKDIR /workspace