#!/usr/bin/env python3
"""
Test complete Raw REPL flow with proper initialization
"""
import subprocess
import time
import sys

def test_raw_repl():
    print("Starting MicroPython unix port...")
    
    proc = subprocess.Popen(
        ["./micropython/ports/unix/build-standard/micropython", "-i"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        bufsize=0
    )
    
    # Read initial banner
    print("Reading initial output...")
    time.sleep(0.5)
    
    # Send interrupt to clear any state
    print("Sending Ctrl-C (\\x03) to interrupt...")
    proc.stdin.write(b"\x03")
    proc.stdin.flush()
    time.sleep(0.2)
    
    # Send Ctrl-A to enter raw REPL
    print("Sending Ctrl-A (\\x01) to enter raw REPL...")
    proc.stdin.write(b"\x01")
    proc.stdin.flush()
    
    # Read response with timeout
    print("Waiting for raw REPL response...")
    output = b""
    start_time = time.time()
    while time.time() - start_time < 2:
        try:
            proc.stdout.flush()
            byte = proc.stdout.read(1)
            if byte:
                output += byte
                sys.stdout.write(f"[{byte.hex()}]")
                sys.stdout.flush()
                if b">" in output and b"raw REPL" in output:
                    print(f"\nGot raw REPL prompt: {output.decode('utf-8', errors='replace')}")
                    break
        except:
            pass
        time.sleep(0.01)
    
    if b"raw REPL" not in output:
        print(f"\nDid not get raw REPL response. Got: {output.decode('utf-8', errors='replace')}")
    else:
        # Test executing code
        print("\nTesting code execution...")
        proc.stdin.write(b"print('hello')\x04")
        proc.stdin.flush()
        
        time.sleep(0.5)
        exec_output = proc.stdout.read(100)
        print(f"Execution output: {exec_output.decode('utf-8', errors='replace')}")
    
    print("\nTerminating...")
    proc.terminate()
    proc.wait()

if __name__ == "__main__":
    test_raw_repl()