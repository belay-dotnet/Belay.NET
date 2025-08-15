#!/usr/bin/env python3
"""
Quick test script to validate file transfer optimizations from Python side.
This helps validate the adaptive chunk sizing is working as expected.
"""

import time
import random

# Test creating and transferring different sized files
test_sizes = [
    ("small", 100),      # 100 bytes - should use small chunks
    ("medium", 2048),    # 2KB - should start adapting
    ("large", 8192),     # 8KB - should use optimized chunks
]

print("🧪 File Transfer Test Data Generator")
print("=" * 50)

for name, size in test_sizes:
    filename = f"test_{name}.dat"
    
    # Create test file with random data
    data = bytes([random.randint(0, 255) for _ in range(size)])
    
    with open(filename, 'wb') as f:
        f.write(data)
    
    print(f"✅ Created {filename}: {size} bytes")

print("\nTest files created. These can be used to manually test file transfer optimizations:")
print("1. Upload files to device using WriteFileAsync")
print("2. Download files back using GetFileAsync") 
print("3. Monitor chunk size adaptation in debug logs")
print("4. Compare transfer times to see optimization effects")