#!/usr/bin/env python3
"""
Simple debug to see what MicroPython outputs during Raw REPL entry
"""
import subprocess
import time

def main():
    print("Testing MicroPython Raw REPL sequence...")
    
    # Start subprocess
    proc = subprocess.Popen(
        ["/home/corona/belay.net/micropython/ports/unix/build-standard/micropython"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0  # No buffering
    )
    
    # Give it time to start
    time.sleep(1)
    
    # Send interrupt (Ctrl-C) with carriage return
    print("Sending \\r\\x03...")
    proc.stdin.write("\r\x03")
    proc.stdin.flush()
    
    # Wait a bit
    time.sleep(0.5)
    
    # Read any startup output
    try:
        # Use non-blocking read
        import fcntl
        import os
        
        # Make stdout non-blocking
        fd = proc.stdout.fileno()
        fl = fcntl.fcntl(fd, fcntl.F_GETFL)
        fcntl.fcntl(fd, fcntl.F_SETFL, fl | os.O_NONBLOCK)
        
        startup_output = ""
        try:
            output = proc.stdout.read()
            if output:
                startup_output += output
                print(f"After interrupt: {repr(startup_output)}")
        except:
            print("No output after interrupt")
    except Exception as e:
        print(f"Error reading startup: {e}")
    
    # Send enter raw mode (Ctrl-A) with carriage return  
    print("Sending \\r\\x01...")
    proc.stdin.write("\r\x01")
    proc.stdin.flush()
    
    # Wait for raw REPL response
    time.sleep(1)
    
    # Try to read the raw REPL response
    try:
        raw_output = proc.stdout.read()
        if raw_output:
            print(f"Raw REPL response: {repr(raw_output)}")
        else:
            print("No raw REPL response received")
    except:
        print("No raw REPL response received")
    
    # Terminate
    proc.terminate()
    proc.wait()

if __name__ == "__main__":
    main()