#!/usr/bin/env python3
"""
Non-blocking subprocess debug
"""
import subprocess
import select
import sys
import time

def test_nonblocking():
    print("Starting MicroPython with non-blocking I/O...")
    
    proc = subprocess.Popen(
        ["./micropython/ports/unix/build-standard/micropython", "-i"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        bufsize=0
    )
    
    # Make stdout/stderr non-blocking
    import fcntl
    import os
    
    def make_nonblocking(fd):
        flags = fcntl.fcntl(fd, fcntl.F_GETFL)
        fcntl.fcntl(fd, fcntl.F_SETFL, flags | os.O_NONBLOCK)
    
    make_nonblocking(proc.stdout)
    make_nonblocking(proc.stderr)
    
    def read_available():
        """Read all available data"""
        data = b""
        try:
            while True:
                chunk = proc.stdout.read(1024)
                if not chunk:
                    break
                data += chunk
        except BlockingIOError:
            pass
        return data.decode('utf-8', errors='replace') if data else ""
    
    # Wait for startup
    print("Waiting for startup...")
    time.sleep(0.5)
    
    startup = read_available()
    if startup:
        print(f"Startup: {startup.replace(chr(13), '\\\\r').replace(chr(10), '\\\\n')}")
    
    # Send Ctrl-A
    print("Sending Ctrl-A...")
    proc.stdin.write(b"\x01")
    proc.stdin.flush()
    
    # Wait and read response
    time.sleep(0.3)
    response = read_available()
    print(f"Raw REPL response: {response.replace(chr(13), '\\\\r').replace(chr(10), '\\\\n')}")
    
    if "raw REPL" in response:
        print("✓ Got Raw REPL")
        
        # Test execution
        print("Testing code execution...")
        proc.stdin.write(b"print('test')\x04")
        proc.stdin.flush()
        
        time.sleep(0.2)
        result = read_available()
        print(f"Result: {result.replace(chr(13), '\\\\r').replace(chr(10), '\\\\n')}")
    else:
        print("✗ No Raw REPL response")
    
    proc.terminate()
    proc.wait()

if __name__ == "__main__":
    test_nonblocking()