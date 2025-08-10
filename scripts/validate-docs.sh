#!/bin/bash

# Comprehensive Documentation Validation Script
# Can be run independently or as part of CI/CD pipeline

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_header() {
    echo ""
    echo -e "${BLUE}=== $1 ===${NC}"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

# Configuration
DOCS_DIR="docs"
EXIT_CODE=0

print_header "Belay.NET Documentation Validation"

# Check if docs directory exists
if [ ! -d "$DOCS_DIR" ]; then
    print_error "Documentation directory '$DOCS_DIR' not found"
    exit 1
fi

cd "$DOCS_DIR"

# 1. Package and Dependencies Check
print_header "Package and Dependencies Check"

if [ ! -f "package.json" ]; then
    print_error "No package.json found - VitePress setup required"
    EXIT_CODE=1
else
    print_success "package.json found"
    
    # Check if dependencies are installed
    if [ ! -d "node_modules" ]; then
        print_info "Installing documentation dependencies..."
        npm install
        if [ $? -eq 0 ]; then
            print_success "Dependencies installed successfully"
        else
            print_error "Failed to install dependencies"
            EXIT_CODE=1
        fi
    else
        print_success "Dependencies already installed"
    fi
fi

# 2. VitePress Configuration Validation
print_header "VitePress Configuration Validation"

if [ ! -f ".vitepress/config.ts" ]; then
    print_error "VitePress configuration file not found"
    EXIT_CODE=1
else
    print_success "VitePress configuration found"
    
    # Check for basic required fields
    if grep -q "title:" ".vitepress/config.ts" && grep -q "description:" ".vitepress/config.ts"; then
        print_success "Basic configuration fields present"
    else
        print_warning "Some basic configuration fields may be missing"
    fi
    
    # Check sidebar configuration
    if grep -q "sidebar:" ".vitepress/config.ts"; then
        print_success "Sidebar navigation configured"
    else
        print_warning "No sidebar navigation found"
    fi
fi

# 3. VitePress Build Test
print_header "VitePress Build Test"

if [ -f "package.json" ] && [ -d "node_modules" ]; then
    print_info "Running VitePress build test..."
    
    # Create temporary build log
    BUILD_LOG=$(mktemp)
    
    if npm run build > "$BUILD_LOG" 2>&1; then
        print_success "VitePress build completed successfully"
        
        # Check for warnings in build output
        WARNING_COUNT=$(grep -c "warning\|warn" "$BUILD_LOG" || true)
        if [ "$WARNING_COUNT" -gt 0 ]; then
            print_warning "Build completed with $WARNING_COUNT warnings"
        fi
    else
        print_error "VitePress build failed"
        echo ""
        echo "Build output:"
        cat "$BUILD_LOG"
        EXIT_CODE=1
    fi
    
    # Clean up build log
    rm -f "$BUILD_LOG"
else
    print_warning "Skipping build test - dependencies not available"
fi

# 4. Markdown File Structure Validation
print_header "Markdown File Structure Validation"

MD_FILES=$(find . -name "*.md" -not -path "./node_modules/*" -not -path "./.vitepress/*" | wc -l)
print_info "Found $MD_FILES markdown files"

# Check for files with no frontmatter (may need it for VitePress)
FILES_NO_FRONTMATTER=0
while IFS= read -r -d '' file; do
    if [ -s "$file" ] && ! head -n 1 "$file" | grep -q "^---"; then
        if [ $FILES_NO_FRONTMATTER -eq 0 ]; then
            print_warning "Files without frontmatter (may need for VitePress):"
        fi
        echo "  • $file"
        FILES_NO_FRONTMATTER=$((FILES_NO_FRONTMATTER + 1))
    fi
done < <(find . -name "*.md" -not -path "./node_modules/*" -not -path "./.vitepress/*" -print0)

if [ $FILES_NO_FRONTMATTER -eq 0 ]; then
    print_success "All markdown files have frontmatter"
fi

# 5. Link Validation
print_header "Link Validation"

# Check for broken internal links (basic check)
BROKEN_LINKS=0
while IFS= read -r -d '' file; do
    # Find markdown links [text](path)
    while IFS= read -r line; do
        # Extract relative links (not starting with http)
        echo "$line" | grep -oE '\[([^\]]+)\]\(([^)]+)\)' | while read -r link; do
            path=$(echo "$link" | sed -n 's/.*](\([^)]*\)).*/\1/p')
            
            # Skip external links, anchors, and email links
            if [[ ! "$path" =~ ^https?:// ]] && [[ ! "$path" =~ ^mailto: ]] && [[ ! "$path" =~ ^# ]]; then
                # Convert relative path to absolute path
                dir=$(dirname "$file")
                if [[ "$path" =~ ^\./ ]]; then
                    target_path="$dir/${path#./}"
                elif [[ "$path" =~ ^\. ]]; then
                    target_path="$dir/$path"
                else
                    target_path="$path"
                fi
                
                # Add .md extension if not present and path doesn't point to directory
                if [[ ! "$target_path" =~ \.md$ ]] && [[ ! "$target_path" =~ /$ ]] && [ ! -d "$target_path" ]; then
                    target_path="$target_path.md"
                fi
                
                # Check if target exists
                if [ ! -f "$target_path" ] && [ ! -d "${target_path%/*}" ]; then
                    if [ $BROKEN_LINKS -eq 0 ]; then
                        print_warning "Potentially broken internal links found:"
                    fi
                    echo "  • $file: $link -> $target_path"
                    BROKEN_LINKS=$((BROKEN_LINKS + 1))
                fi
            fi
        done
    done < "$file"
done < <(find . -name "*.md" -not -path "./node_modules/*" -not -path "./.vitepress/*" -print0)

if [ $BROKEN_LINKS -eq 0 ]; then
    print_success "No obvious broken internal links detected"
elif [ $BROKEN_LINKS -gt 10 ]; then
    print_error "Found $BROKEN_LINKS potentially broken links"
    EXIT_CODE=1
else
    print_warning "Found $BROKEN_LINKS potentially broken links"
fi

# 6. Navigation Coverage Check
print_header "Navigation Coverage Check"

if [ -f ".vitepress/config.ts" ]; then
    # Check for orphaned pages using Python (if available)
    if command -v python3 &> /dev/null; then
        python3 << 'EOF'
import os
import re
import sys

try:
    # Read VitePress config
    with open('.vitepress/config.ts', 'r') as f:
        config_content = f.read()
    
    # Find all markdown files
    md_files = []
    for root, dirs, files in os.walk('.'):
        # Skip node_modules and .vitepress
        dirs[:] = [d for d in dirs if d not in ['node_modules', '.vitepress', 'dist']]
        for file in files:
            if file.endswith('.md') and file != 'index.md':
                rel_path = os.path.relpath(os.path.join(root, file), '.')
                # Convert to URL path (remove .md extension)
                url_path = '/' + rel_path.replace('.md', '').replace(os.sep, '/')
                md_files.append((rel_path, url_path))
    
    # Check if each page is referenced in config
    orphaned_pages = []
    for file_path, url_path in md_files:
        if url_path not in config_content:
            orphaned_pages.append(file_path)
    
    if orphaned_pages:
        print(f"⚠ Found {len(orphaned_pages)} potentially orphaned pages:")
        for page in orphaned_pages:
            print(f"  • {page}")
        print("Consider adding these to .vitepress/config.ts sidebar navigation")
        sys.exit(1)
    else:
        print("✓ All documentation pages appear to be linked in navigation")
        
except Exception as e:
    print(f"⚠ Could not perform navigation coverage check: {e}")
    
EOF
        
        if [ $? -ne 0 ]; then
            print_warning "Navigation coverage check found issues"
        fi
    else
        print_info "Python not available, skipping navigation coverage check"
    fi
fi

# 7. Content Quality Checks
print_header "Content Quality Checks"

# Check for TODO/FIXME markers
TODO_FILES=$(find . -name "*.md" -not -path "./node_modules/*" -exec grep -l "TODO\|FIXME\|XXX" {} \; 2>/dev/null | wc -l)
if [ $TODO_FILES -gt 0 ]; then
    print_warning "Found $TODO_FILES files with TODO/FIXME markers"
    find . -name "*.md" -not -path "./node_modules/*" -exec grep -l "TODO\|FIXME\|XXX" {} \; 2>/dev/null | head -5
    if [ $TODO_FILES -gt 5 ]; then
        echo "  ... and $((TODO_FILES - 5)) more"
    fi
fi

# Check for placeholder content
PLACEHOLDER_FILES=$(find . -name "*.md" -not -path "./node_modules/*" -exec grep -l "Documentation in Progress\|Coming Soon\|Placeholder" {} \; 2>/dev/null | wc -l)
if [ $PLACEHOLDER_FILES -gt 0 ]; then
    print_warning "Found $PLACEHOLDER_FILES files with placeholder content"
fi

print_success "Content quality check completed"

# 8. Final Summary
print_header "Validation Summary"

if [ $EXIT_CODE -eq 0 ]; then
    print_success "All documentation validation checks passed!"
    print_info "Documentation is ready for deployment"
else
    print_error "Documentation validation failed with errors"
    print_info "Please fix the issues above before deploying"
fi

exit $EXIT_CODE