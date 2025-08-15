#!/usr/bin/env python3
import subprocess
import time
import sys

def test_unix_port_raw_repl():
    print("Testing MicroPython unix port raw REPL...")
    
    # Start the unix port
    proc = subprocess.Popen(
        ["./micropython/ports/unix/build-standard/micropython"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=False
    )
    
    try:
        # Test 1: Send some basic input first
        print("Test 1: Basic input test")
        proc.stdin.write(b"print('Hello')\n")
        proc.stdin.flush()
        time.sleep(0.5)
        
        # Read any output
        proc.stdout.settimeout(1)  # This won't work, but let's try
        
        # Test 2: Try raw REPL sequence
        print("Test 2: Raw REPL sequence")
        proc.stdin.write(b"\r\x03")  # Ctrl-C
        proc.stdin.flush()
        time.sleep(0.1)
        
        proc.stdin.write(b"\r\x01")  # Ctrl-A  
        proc.stdin.flush()
        time.sleep(0.5)
        
        # Try to read response
        print("Attempting to read response...")
        try:
            output = proc.stdout.read(1024)
            if output:
                print(f"Got output: {output}")
            else:
                print("No output received")
        except:
            print("Failed to read output")
            
    except Exception as e:
        print(f"Error: {e}")
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    test_unix_port_raw_repl()