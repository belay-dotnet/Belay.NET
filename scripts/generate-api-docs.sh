#!/bin/bash

# Generate API documentation from XML comments
# Uses DocFX or similar tool to create markdown from XML documentation

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info "Generating API documentation from XML comments..."

# Check if we're in the project root
if [ ! -f "Belay.NET.sln" ]; then
    print_error "Must be run from project root (where Belay.NET.sln exists)"
    exit 1
fi

# Create API docs directory if it doesn't exist
mkdir -p docs/api/generated

# Build the project to ensure XML files are up to date
print_info "Building project to generate latest XML documentation..."
dotnet build --configuration Release --verbosity quiet
print_success "Project built successfully"

# Check for XML documentation files
XML_FILES=$(find src/*/bin/Release/net8.0 -name "*.xml" -not -path "*/ref/*" 2>/dev/null || true)
if [ -z "$XML_FILES" ]; then
    print_error "No XML documentation files found. Ensure projects have GenerateDocumentationFile enabled."
    exit 1
fi

XML_COUNT=$(echo "$XML_FILES" | wc -l)
print_success "Found $XML_COUNT XML documentation files"

# Generate API documentation using xmldoc2md (if available)
if command -v xmldoc2md &> /dev/null; then
    print_info "Using xmldoc2md to generate markdown documentation..."
    
    for xml_file in $XML_FILES; do
        assembly_name=$(basename "$xml_file" .xml)
        output_dir="docs/api/generated/$assembly_name"
        
        print_info "Generating docs for $assembly_name..."
        mkdir -p "$output_dir"
        xmldoc2md "$xml_file" "$output_dir" --index-page-name "README"
        print_success "Generated documentation for $assembly_name"
    done
    
elif command -v docfx &> /dev/null; then
    print_info "Using DocFX to generate documentation..."
    
    # Create DocFX configuration if it doesn't exist
    if [ ! -f "docfx.json" ]; then
        print_info "Creating DocFX configuration..."
        cat > docfx.json << 'EOF'
{
  "metadata": [
    {
      "src": [
        {
          "files": [ "src/**.csproj" ],
          "exclude": [ "**/bin/**", "**/obj/**" ]
        }
      ],
      "dest": "docs/api/generated",
      "includePrivateMembers": false,
      "disableGitFeatures": false,
      "disableDefaultFilter": false,
      "noRestore": false,
      "namespaceLayout": "flattened",
      "memberLayout": "samePage"
    }
  ],
  "build": {
    "content": [
      {
        "files": [ "docs/api/generated/**.yml", "docs/api/generated/index.md" ]
      }
    ],
    "resource": [
      {
        "files": [ "images/**" ]
      }
    ],
    "overwrite": [
      {
        "files": [ "apidoc/**.md" ],
        "exclude": [ "obj/**", "_site/**" ]
      }
    ],
    "dest": "_site",
    "globalMetadataFiles": [],
    "fileMetadataFiles": [],
    "template": [ "default" ],
    "postProcessors": [],
    "markdownEngineName": "markdig",
    "noLangKeyword": false,
    "keepFileLink": false,
    "cleanupCacheHistory": false,
    "disableGitFeatures": false
  }
}
EOF
        print_success "Created DocFX configuration"
    fi
    
    # Generate metadata and build docs
    docfx metadata docfx.json
    print_success "Generated API metadata with DocFX"
    
else
    print_warning "No documentation generation tool found (xmldoc2md or docfx)"
    print_info "Falling back to manual XML processing..."
    
    # Simple XML to markdown converter using Python (if available)
    if command -v python3 &> /dev/null; then
        print_info "Using Python to process XML documentation..."
        
        python3 << 'EOF'
import xml.etree.ElementTree as ET
import os
import re
from pathlib import Path

def clean_xml_text(text):
    """Clean up XML text content"""
    if not text:
        return ""
    # Remove extra whitespace and normalize
    text = re.sub(r'\s+', ' ', text.strip())
    return text

def process_xml_file(xml_path):
    """Convert XML documentation to markdown"""
    try:
        tree = ET.parse(xml_path)
        root = tree.getroot()
        
        assembly_name = root.find('assembly/name').text
        output_dir = Path(f"docs/api/generated/{assembly_name}")
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Create main index file
        with open(output_dir / "README.md", "w") as f:
            f.write(f"# {assembly_name} API Reference\n\n")
            f.write("Generated from XML documentation comments.\n\n")
            
            # Group members by type
            types = {}
            for member in root.findall('.//member'):
                name = member.get('name', '')
                if name.startswith('T:'):  # Type
                    type_name = name[2:]
                    summary_elem = member.find('summary')
                    summary = clean_xml_text(summary_elem.text if summary_elem is not None else "")
                    
                    f.write(f"## {type_name}\n\n")
                    if summary:
                        f.write(f"{summary}\n\n")
                    
                    # Find all members of this type
                    type_prefix = name[2:] + "."
                    members = []
                    
                    for m in root.findall('.//member'):
                        m_name = m.get('name', '')
                        if (m_name.startswith(f'M:{type_prefix}') or 
                            m_name.startswith(f'P:{type_prefix}') or 
                            m_name.startswith(f'F:{type_prefix}')):
                            
                            member_type = m_name[0]  # M, P, or F
                            member_name = m_name[2:]  # Remove prefix
                            
                            summary_elem = m.find('summary')
                            summary = clean_xml_text(summary_elem.text if summary_elem is not None else "")
                            
                            type_str = {"M": "Method", "P": "Property", "F": "Field"}.get(member_type, "Member")
                            f.write(f"### {member_name} ({type_str})\n\n")
                            if summary:
                                f.write(f"{summary}\n\n")
                            
                            # Add parameters if method
                            for param in m.findall('param'):
                                param_name = param.get('name', '')
                                param_text = clean_xml_text(param.text or '')
                                if param_name and param_text:
                                    f.write(f"**{param_name}**: {param_text}\n\n")
                            
                            # Add returns if present
                            returns = m.find('returns')
                            if returns is not None and returns.text:
                                returns_text = clean_xml_text(returns.text)
                                f.write(f"**Returns**: {returns_text}\n\n")
                            
                            # Add remarks if present
                            remarks = m.find('remarks')
                            if remarks is not None and remarks.text:
                                remarks_text = clean_xml_text(remarks.text)
                                f.write(f"**Remarks**: {remarks_text}\n\n")
                    
                    f.write("---\n\n")
        
        print(f"✓ Generated documentation for {assembly_name}")
        return True
        
    except Exception as e:
        print(f"✗ Error processing {xml_path}: {e}")
        return False

# Process all XML files
import glob
xml_files = glob.glob("src/*/bin/Release/net8.0/*.xml")
success_count = 0

for xml_file in xml_files:
    if "ref" not in xml_file:  # Skip reference assemblies
        if process_xml_file(xml_file):
            success_count += 1

print(f"✓ Successfully processed {success_count}/{len(xml_files)} XML documentation files")
EOF
        print_success "Generated API documentation using Python XML parser"
    else
        print_error "No suitable tools available for API documentation generation"
        print_info "Please install one of: xmldoc2md, docfx, or python3"
        exit 1
    fi
fi

# Update main API index to include generated docs
if [ -d "docs/api/generated" ]; then
    print_info "Updating main API index page..."
    
    # Create comprehensive index
    cat > docs/api/index.md << 'EOF'
# API Reference

The Belay.NET API documentation provides comprehensive information about all public classes, methods, and interfaces.

## Generated Documentation

The following API documentation is automatically generated from XML comments in the source code:

EOF

    # Add links to generated documentation
    for dir in docs/api/generated/*/; do
        if [ -d "$dir" ]; then
            assembly_name=$(basename "$dir")
            echo "- [$assembly_name](./$assembly_name/README.md)" >> docs/api/index.md
        fi
    done
    
    cat >> docs/api/index.md << 'EOF'

## Quick Reference

### Core Classes
- **Device** - Main device connection and communication
- **TaskExecutor** - Handles [Task] attribute methods  
- **EnhancedExecutor** - Advanced method interception framework
- **DeviceProxy** - Dynamic proxy for transparent method routing

### Attributes
- **TaskAttribute** - Execute methods as tasks with caching and timeout
- **ThreadAttribute** - Background thread execution
- **SetupAttribute** - Device initialization methods
- **TeardownAttribute** - Device cleanup methods

For detailed documentation, see the generated API reference above.

## Usage Examples

For practical examples of using these APIs, see the [Examples](/examples/) section.
EOF

    print_success "Updated main API index page"
    
    # Remove the warning since we now have generated docs
    if grep -q "Documentation Status" docs/api/index.md; then
        print_info "Removing documentation status warning..."
        sed -i '/## Documentation Status/,/:::/d' docs/api/index.md
        print_success "Documentation status warning removed"
    fi
fi

print_success "API documentation generation completed!"

# Show summary
if [ -d "docs/api/generated" ]; then
    GENERATED_COUNT=$(find docs/api/generated -name "README.md" | wc -l)
    print_info "Generated documentation for $GENERATED_COUNT assemblies:"
    find docs/api/generated -name "README.md" | sed 's|docs/api/generated/||; s|/README.md||' | sort | sed 's/^/  • /'
fi