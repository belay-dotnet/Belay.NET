#!/usr/bin/env python3
"""
Debug script to test MicroPython subprocess communication similar to our C# implementation
"""

import subprocess
import time
import threading

def main():
    print("Starting MicroPython subprocess...")
    
    # Start subprocess similar to our C# implementation
    proc = subprocess.Popen(
        ["/home/corona/belay.net/micropython/ports/unix/build-standard/micropython"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0  # No buffering
    )
    
    print(f"Process started with PID: {proc.pid}")
    
    def read_output():
        """Read output in separate thread"""
        try:
            while True:
                line = proc.stdout.readline()
                if not line:
                    break
                print(f"[OUT] {line.rstrip()}")
        except Exception as e:
            print(f"Error reading output: {e}")
    
    def read_error():
        """Read error output in separate thread"""
        try:
            while True:
                line = proc.stderr.readline()
                if not line:
                    break
                print(f"[ERR] {line.rstrip()}")
        except Exception as e:
            print(f"Error reading stderr: {e}")
    
    # Start monitoring threads
    out_thread = threading.Thread(target=read_output, daemon=True)
    err_thread = threading.Thread(target=read_error, daemon=True)
    out_thread.start()
    err_thread.start()
    
    print("Waiting for startup...")
    time.sleep(2)
    
    print("Sending interrupt (Ctrl-C)...")
    proc.stdin.write("\x03")
    proc.stdin.flush()
    
    time.sleep(0.5)
    
    print("Sending enter raw mode (Ctrl-A)...")
    proc.stdin.write("\x01")  
    proc.stdin.flush()
    
    time.sleep(0.5)
    
    print("Testing simple command: 1+1...")
    proc.stdin.write("1+1\n")
    proc.stdin.flush()
    
    time.sleep(1)
    
    print("Sending Ctrl-D to execute...")
    proc.stdin.write("\x04")
    proc.stdin.flush()
    
    time.sleep(2)
    
    print("Terminating process...")
    proc.terminate()
    proc.wait()
    print("Done!")

if __name__ == "__main__":
    main()