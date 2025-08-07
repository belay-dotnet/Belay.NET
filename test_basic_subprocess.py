#!/usr/bin/env python3
"""
Very basic subprocess test to verify MicroPython is responding
"""
import subprocess
import time
import sys

proc = subprocess.Popen(
    ["./micropython/ports/unix/build-standard/micropython", "-i"],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    bufsize=0
)

print("Process started, waiting for initial output...")
time.sleep(0.5)

# Make non-blocking
import fcntl
import os
flags = fcntl.fcntl(proc.stdout, fcntl.F_GETFL)
fcntl.fcntl(proc.stdout, fcntl.F_SETFL, flags | os.O_NONBLOCK)

# Read whatever is available
try:
    startup = proc.stdout.read()
    print(f"Startup output: {startup.decode('utf-8', errors='replace') if startup else 'None'}")
except BlockingIOError:
    print("No startup output available")

print("Sending simple newline...")
proc.stdin.write(b"\n")
proc.stdin.flush()
time.sleep(0.1)

try:
    response = proc.stdout.read()
    print(f"Response: {response.decode('utf-8', errors='replace') if response else 'None'}")
except BlockingIOError:
    print("No response available")

print("Sending Ctrl-A...")
proc.stdin.write(b"\x01")
proc.stdin.flush()
time.sleep(0.2)

try:
    raw_response = proc.stdout.read()
    print(f"Raw REPL response: {raw_response.decode('utf-8', errors='replace') if raw_response else 'None'}")
except BlockingIOError:
    print("No raw REPL response available")

proc.terminate()
proc.wait()
print("Done")