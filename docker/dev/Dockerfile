# docker/dev/Dockerfile  
FROM ghcr.io/belay-dotnet/belay-test-base:latest AS dev

# Metadata
LABEL org.opencontainers.image.source="https://github.com/belay-dotnet/Belay.NET"
LABEL org.opencontainers.image.description="Belay.NET Development Environment"

# Install additional development tools
USER root
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    unzip \
    vim \
    nano \
    htop \
    tree \
    jq \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

# Install Node.js for documentation development
RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs

# Install DocFX for documentation generation
RUN curl -L https://github.com/dotnet/docfx/releases/download/v2.78.3/docfx-linux-x64-v2.78.3.zip -o docfx.zip \
    && unzip docfx.zip -d /usr/local/bin \
    && chmod +x /usr/local/bin/docfx \
    && rm docfx.zip

# Install Python tools for documentation processing
RUN apt-get update && apt-get install -y python3-pip \
    && pip3 install --no-cache-dir markdown pyyaml jinja2 \
    && rm -rf /var/lib/apt/lists/*

# Install global npm packages for development (as root)
RUN npm install -g \
    vitepress@latest \
    markdown-it \
    @types/node

USER builder

# Install additional .NET development tools
RUN dotnet tool install -g dotnet-ef \
    && dotnet tool install -g dotnet-watch \
    && dotnet tool install -g dotnet-format

# Environment variables for development
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=true

# Verify all tools are working
RUN dotnet --version && \
    micropython --version && \
    node --version && \
    docfx --version && \
    echo "Development environment ready"

WORKDIR /workspace

# Health check that validates all major components
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD dotnet --version && micropython -c "print('OK')" && node --version || exit 1