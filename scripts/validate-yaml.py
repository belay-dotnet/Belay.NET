#!/usr/bin/env python3
"""
Validate YAML files for syntax errors
"""

import yaml
import sys
import os
from pathlib import Path

def validate_yaml_file(file_path):
    """Validate a single YAML file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            yaml.safe_load(f)
        print(f"✅ {file_path}: Valid YAML")
        return True
    except yaml.YAMLError as e:
        print(f"❌ {file_path}: YAML Error - {e}")
        return False
    except Exception as e:
        print(f"❌ {file_path}: Error - {e}")
        return False

def main():
    """Main validation function"""
    if len(sys.argv) < 2:
        # Find all YAML files if no arguments provided
        yaml_files = []
        for pattern in ['**/*.yml', '**/*.yaml']:
            yaml_files.extend(Path('.').glob(pattern))
        
        # Filter out excluded directories
        excluded_dirs = {'micropython', '.vitepress/dist', 'node_modules', 'bin', 'obj'}
        yaml_files = [f for f in yaml_files if not any(exc in str(f) for exc in excluded_dirs)]
    else:
        yaml_files = [Path(f) for f in sys.argv[1:]]
    
    if not yaml_files:
        print("No YAML files found to validate")
        return 0
    
    print(f"🔍 Validating {len(yaml_files)} YAML files...")
    
    valid_count = 0
    for yaml_file in yaml_files:
        if yaml_file.exists():
            if validate_yaml_file(yaml_file):
                valid_count += 1
        else:
            print(f"⚠️  {yaml_file}: File not found")
    
    failed_count = len(yaml_files) - valid_count
    
    if failed_count == 0:
        print(f"🎉 All {valid_count} YAML files are valid!")
        return 0
    else:
        print(f"💥 {failed_count} YAML file(s) failed validation")
        return 1

if __name__ == "__main__":
    sys.exit(main())