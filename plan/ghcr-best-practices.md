# GitHub Container Registry (GHCR) Best Practices for Belay.NET

**Date**: 2025-08-12  
**Status**: 📋 REFERENCE DOCUMENT  
**Purpose**: Guide for implementing GHCR in Belay.NET project

## Overview

GitHub Container Registry (ghcr.io) provides native container hosting integrated with GitHub repositories. This document outlines best practices specific to the Belay.NET project's needs for hosting build dependency containers.

## Registry Organization

### Naming Conventions

#### Image Naming Structure
```
ghcr.io/<namespace>/<image-name>:<tag>
ghcr.io/belay-dotnet/belay-build-base:latest
ghcr.io/belay-dotnet/belay-build-base:v1.0.0
ghcr.io/belay-dotnet/belay-build-base:sha-7d3f4a2
```

#### Recommended Naming Pattern
- **Namespace**: Use organization name (`belay-dotnet`)
- **Image Name**: Descriptive, lowercase, hyphenated
- **Tag Strategy**:
  - `latest` - Most recent stable version
  - `v1.0.0` - Semantic versioning for releases
  - `sha-<commit>` - Git SHA for traceability
  - `pr-<number>` - Pull request builds
  - `nightly` - Automated nightly builds

### Package Visibility

#### Public vs Private
```yaml
# For open source projects - PUBLIC recommended
- name: Make container public
  run: |
    gh api \
      --method PATCH \
      -H "Accept: application/vnd.github+json" \
      /user/packages/container/<package_name>/visibility \
      -f visibility='public'
```

#### Benefits of Public Packages
- No authentication required for pulls
- Faster CI/CD workflows
- Community contribution friendly
- No rate limiting for anonymous pulls

## Authentication and Access Control

### GitHub Actions Authentication

#### Recommended Approach
```yaml
- name: Log in to GHCR
  uses: docker/login-action@v3
  with:
    registry: ghcr.io
    username: ${{ github.actor }}
    password: ${{ secrets.GITHUB_TOKEN }}
```

#### Personal Access Token (PAT) for Local Development
```bash
# Create PAT with packages:write scope
echo $CR_PAT | docker login ghcr.io -u USERNAME --password-stdin

# Alternative: Use GitHub CLI
gh auth token | docker login ghcr.io -u USERNAME --password-stdin
```

### Repository Permissions

#### Package Settings
```yaml
# .github/package.yml
packages:
  - name: belay-build-base
    visibility: public
    permissions:
      admin: ["belay-dotnet/maintainers"]
      write: ["belay-dotnet/developers"]
      read: ["public"]
```

## Image Lifecycle Management

### Retention Policies

#### Automated Cleanup
```yaml
# .github/workflows/cleanup-packages.yml
name: Delete old container images

on:
  schedule:
    - cron: '0 1 * * 0'  # Weekly on Sunday

jobs:
  cleanup:
    runs-on: ubuntu-latest
    steps:
      - name: Delete old images
        uses: actions/delete-package-versions@v4
        with:
          package-name: 'belay-build-base'
          package-type: 'container'
          min-versions-to-keep: 5
          delete-only-pre-release-versions: false
          ignore-versions: '^(latest|v\d+\.\d+\.\d+)$'
```

#### Retention Strategy
- **Keep Forever**: Tagged releases (v1.0.0)
- **Keep 30 days**: SHA-tagged builds
- **Keep 7 days**: PR builds
- **Keep 5 versions**: Latest builds
- **Delete**: Untagged manifests

### Version Management

#### Semantic Versioning
```bash
# Major version (breaking changes)
docker tag image ghcr.io/belay-dotnet/belay-build-base:v2.0.0
docker tag image ghcr.io/belay-dotnet/belay-build-base:v2

# Minor version (new features)
docker tag image ghcr.io/belay-dotnet/belay-build-base:v1.1.0
docker tag image ghcr.io/belay-dotnet/belay-build-base:v1

# Patch version (bug fixes)
docker tag image ghcr.io/belay-dotnet/belay-build-base:v1.0.1
docker tag image ghcr.io/belay-dotnet/belay-build-base:latest
```

## Security Best Practices

### Container Signing with Cosign

#### Setup
```bash
# Install cosign
brew install cosign  # macOS
apt install cosign   # Ubuntu

# Generate keys
cosign generate-key-pair

# Store keys in GitHub Secrets
# COSIGN_PRIVATE_KEY
# COSIGN_PASSWORD
```

#### Signing Workflow
```yaml
- name: Install Cosign
  uses: sigstore/cosign-installer@v3
  
- name: Sign container image
  env:
    COSIGN_PRIVATE_KEY: ${{ secrets.COSIGN_PRIVATE_KEY }}
    COSIGN_PASSWORD: ${{ secrets.COSIGN_PASSWORD }}
  run: |
    echo "$COSIGN_PRIVATE_KEY" > cosign.key
    cosign sign --key cosign.key ghcr.io/belay-dotnet/belay-build-base:${{ github.sha }}
    rm cosign.key
```

### Vulnerability Scanning

#### Trivy Integration
```yaml
- name: Run Trivy vulnerability scanner
  uses: aquasecurity/trivy-action@master
  with:
    image-ref: 'ghcr.io/belay-dotnet/belay-build-base:latest'
    format: 'sarif'
    output: 'trivy-results.sarif'
    severity: 'CRITICAL,HIGH'
    
- name: Upload Trivy results to GitHub Security
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: 'trivy-results.sarif'
```

#### Dependency Scanning
```yaml
- name: Generate SBOM
  uses: anchore/sbom-action@v0
  with:
    image: 'ghcr.io/belay-dotnet/belay-build-base:latest'
    format: 'spdx-json'
    output-file: 'sbom.json'
    
- name: Upload SBOM
  uses: actions/upload-artifact@v4
  with:
    name: sbom
    path: sbom.json
```

### Secret Management

#### Build-time Secrets
```dockerfile
# Use BuildKit secrets (never stored in image)
# syntax=docker/dockerfile:1
FROM base AS build
RUN --mount=type=secret,id=github_token \
    TOKEN=$(cat /run/secrets/github_token) && \
    git clone https://${TOKEN}@github.com/private/repo
```

#### Runtime Configuration
```yaml
# Use environment variables, not embedded secrets
environment:
  - CONFIG_PATH=/config/settings.json
  - LOG_LEVEL=info
  # Never: - API_KEY=secret123
```

## Performance Optimization

### Layer Caching

#### GitHub Actions Cache
```yaml
- name: Set up Docker Buildx
  uses: docker/setup-buildx-action@v3
  
- name: Build and push
  uses: docker/build-push-action@v5
  with:
    context: .
    push: true
    tags: ghcr.io/belay-dotnet/belay-build-base:latest
    cache-from: type=gha
    cache-to: type=gha,mode=max
    platforms: linux/amd64,linux/arm64
```

#### Registry Cache
```yaml
cache-from: |
  type=registry,ref=ghcr.io/belay-dotnet/belay-build-base:buildcache
cache-to: |
  type=registry,ref=ghcr.io/belay-dotnet/belay-build-base:buildcache,mode=max
```

### Multi-Platform Builds

#### Building for Multiple Architectures
```yaml
- name: Set up QEMU
  uses: docker/setup-qemu-action@v3
  
- name: Build multi-platform image
  uses: docker/build-push-action@v5
  with:
    platforms: linux/amd64,linux/arm64,linux/arm/v7
    push: true
    tags: ghcr.io/belay-dotnet/belay-build-base:latest
```

### Image Size Optimization

#### Best Practices
```dockerfile
# Multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# Runtime image (smaller)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "app.dll"]
```

#### Size Monitoring
```yaml
- name: Check image size
  run: |
    SIZE=$(docker manifest inspect ghcr.io/belay-dotnet/belay-build-base:latest \
      | jq '.config.size + .layers[].size' | jq -s 'add' | numfmt --to=iec)
    echo "Image size: $SIZE"
    
    # Fail if over 1GB
    if [ $(echo $SIZE | sed 's/[^0-9]*//g') -gt 1073741824 ]; then
      echo "❌ Image too large: $SIZE"
      exit 1
    fi
```

## Monitoring and Observability

### Package Insights

#### GitHub API Queries
```bash
# Get package metadata
gh api \
  -H "Accept: application/vnd.github+json" \
  /orgs/belay-dotnet/packages/container/belay-build-base

# Get download statistics
gh api \
  -H "Accept: application/vnd.github+json" \
  /orgs/belay-dotnet/packages/container/belay-build-base/stats/downloads

# List all versions
gh api \
  -H "Accept: application/vnd.github+json" \
  /orgs/belay-dotnet/packages/container/belay-build-base/versions
```

### Metrics Dashboard

#### Recommended Metrics
- Download count per version
- Image size trends
- Build success rate
- Vulnerability count
- Storage usage

#### Implementation
```yaml
# .github/workflows/metrics.yml
- name: Collect metrics
  run: |
    # Download count
    DOWNLOADS=$(gh api /orgs/belay-dotnet/packages/container/belay-build-base/stats/downloads | jq '.downloads')
    
    # Storage usage
    STORAGE=$(gh api /orgs/belay-dotnet/packages | jq '[.[] | .size_in_bytes] | add')
    
    # Output to dashboard
    echo "Downloads: $DOWNLOADS" >> metrics.txt
    echo "Storage: $(echo $STORAGE | numfmt --to=iec)" >> metrics.txt
```

## Cost Optimization

### Free Tier Limits
- **Public repositories**: Unlimited storage for public packages
- **Private repositories**: 500MB storage, 1GB bandwidth/month (free tier)
- **GitHub Actions**: 2000 minutes/month (free tier)

### Cost Reduction Strategies

#### 1. Public Packages
- Make build containers public (no storage costs)
- Only keep application images private if needed

#### 2. Efficient Caching
```yaml
# Use GitHub Actions cache instead of registry layers
cache-from: type=gha
cache-to: type=gha,mode=max
```

#### 3. Cleanup Policies
```yaml
# Aggressive cleanup for non-release builds
- name: Delete PR images after merge
  if: github.event.pull_request.merged == true
  run: |
    gh api \
      --method DELETE \
      /orgs/belay-dotnet/packages/container/belay-build-base/versions/$VERSION_ID
```

## Migration Strategy

### From Docker Hub
```bash
# Pull from Docker Hub
docker pull username/image:tag

# Retag for GHCR
docker tag username/image:tag ghcr.io/belay-dotnet/image:tag

# Push to GHCR
docker push ghcr.io/belay-dotnet/image:tag
```

### From Self-Hosted Registry
```bash
# Use docker save/load for air-gapped migration
docker save image:tag | gzip > image.tar.gz
# Transfer file
docker load < image.tar.gz
docker tag image:tag ghcr.io/belay-dotnet/image:tag
docker push ghcr.io/belay-dotnet/image:tag
```

## Troubleshooting

### Common Issues

#### Authentication Failures
```bash
# Error: unauthorized
# Solution: Refresh token
docker logout ghcr.io
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin
```

#### Rate Limiting
```bash
# Check rate limit status
curl -H "Authorization: token $GITHUB_TOKEN" \
  https://api.github.com/rate_limit

# Solution: Use authenticated requests
docker login ghcr.io
```

#### Manifest Unknown
```bash
# Error: manifest unknown
# Solution: Check image exists and is public
docker manifest inspect ghcr.io/belay-dotnet/belay-build-base:latest

# Or make public
gh api --method PATCH \
  /user/packages/container/belay-build-base/visibility \
  -f visibility='public'
```

## Compliance and Governance

### License Compliance
```dockerfile
# Include licenses in image
COPY LICENSE /licenses/
COPY --from=build /app/licenses/* /licenses/

# Add labels
LABEL org.opencontainers.image.licenses="MIT"
```

### Supply Chain Security
```yaml
# Generate and attach attestation
- name: Generate SLSA provenance
  uses: slsa-framework/slsa-github-generator@v1.5.0
  with:
    image: ghcr.io/belay-dotnet/belay-build-base
    digest: ${{ steps.build.outputs.digest }}
```

### Audit Logging
```bash
# Enable audit log streaming
gh api --method PUT \
  /orgs/belay-dotnet/audit-log/streams \
  -f provider='datadog' \
  -f config='{"endpoint":"https://intake.logs.datadoghq.com"}'
```

## Integration Examples

### GitHub Actions Integration
```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
      
    steps:
      - uses: actions/checkout@v4
      
      - name: Build and push to GHCR
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: |
            ghcr.io/belay-dotnet/app:latest
            ghcr.io/belay-dotnet/app:${{ github.sha }}
```

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: belay-app
spec:
  template:
    spec:
      imagePullSecrets:
        - name: ghcr-secret
      containers:
        - name: app
          image: ghcr.io/belay-dotnet/app:latest
          imagePullPolicy: Always
```

### Docker Compose
```yaml
services:
  app:
    image: ghcr.io/belay-dotnet/belay-build-base:latest
    environment:
      - ENV=production
```

## Conclusion

Following these GHCR best practices will ensure secure, efficient, and cost-effective container management for the Belay.NET project. Key takeaways:

1. **Use public packages** for build dependencies to eliminate storage costs
2. **Implement signing and scanning** for security
3. **Optimize caching** to reduce build times
4. **Automate cleanup** to manage storage
5. **Monitor usage** to track costs and performance

Regular review and updates of these practices will ensure continued optimization as the project grows.