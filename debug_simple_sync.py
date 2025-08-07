#!/usr/bin/env python3
"""
Simple synchronous debug test
"""
import subprocess
import time
import select
import os

def test_simple():
    print("Starting MicroPython...")
    proc = subprocess.Popen(
        ["./micropython/ports/unix/build-standard/micropython", "-i"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        bufsize=0
    )
    
    # Make stdout non-blocking
    import fcntl
    fd = proc.stdout.fileno()
    fl = fcntl.fcntl(fd, fcntl.F_GETFL)
    fcntl.fcntl(fd, fcntl.F_SETFL, fl | os.O_NONBLOCK)
    
    print("Reading startup output...")
    time.sleep(0.3)  # Let startup complete
    
    try:
        startup = proc.stdout.read()
        print(f"Startup: {startup.decode('utf-8', errors='replace')}")
    except:
        print("No startup output")
    
    print("Sending Ctrl-A...")
    proc.stdin.write(b"\x01")
    proc.stdin.flush()
    
    time.sleep(0.2)
    
    try:
        response = proc.stdout.read()
        print(f"Raw REPL response: {response.decode('utf-8', errors='replace')}")
    except:
        print("No Raw REPL response")
    
    proc.terminate()
    proc.wait()

if __name__ == "__main__":
    test_simple()