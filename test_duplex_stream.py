#!/usr/bin/env python3
"""
Test if our DuplexStream approach works by simulating the exact same setup in Python
"""
import subprocess
import time
import io

class PythonDuplexStream:
    def __init__(self, input_stream, output_stream):
        self.input_stream = input_stream
        self.output_stream = output_stream
    
    def write(self, data):
        self.input_stream.write(data)
        self.input_stream.flush()
    
    def read(self, size):
        return self.output_stream.read(size)

def test_duplex_approach():
    print("Starting MicroPython subprocess...")
    proc = subprocess.Popen(
        ["./micropython/ports/unix/build-standard/micropython", "-i"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        bufsize=0
    )
    
    # Create our duplex stream (similar to C#)
    duplex = PythonDuplexStream(proc.stdin, proc.stdout)
    
    print("Waiting for startup...")
    time.sleep(1.0)
    
    # Make stdout non-blocking for available data check
    import fcntl
    import os
    flags = fcntl.fcntl(proc.stdout, fcntl.F_GETFL)
    fcntl.fcntl(proc.stdout, fcntl.F_SETFL, flags | os.O_NONBLOCK)
    
    # Drain startup (similar to DrainAvailableOutputAsync)
    print("Draining startup output...")
    while True:
        try:
            data = proc.stdout.read(1024)
            if not data:
                break
            print(f"Drained: {data.decode('utf-8', errors='replace').replace(chr(13), '\\\\r').replace(chr(10), '\\\\n')}")
        except BlockingIOError:
            break
    
    # Send interrupt (similar to InitializeAsync)
    print("Sending interrupt...")
    duplex.write(b"\r\x03")
    time.sleep(0.1)
    
    # Drain interrupt response
    while True:
        try:
            data = proc.stdout.read(1024)
            if not data:
                break
            print(f"Interrupt response: {data.decode('utf-8', errors='replace').replace(chr(13), '\\\\r').replace(chr(10), '\\\\n')}")
        except BlockingIOError:
            break
    
    # Now try to enter Raw REPL (similar to EnterRawModeAsync)
    print("Sending Ctrl-A...")
    duplex.write(b"\x01")
    time.sleep(0.2)
    
    # Read response with timeout (similar to ReadWithTimeoutAsync)
    print("Reading Raw REPL response...")
    start_time = time.time()
    response = b""
    while time.time() - start_time < 2.0:
        try:
            data = proc.stdout.read(1024)
            if data:
                response += data
                text = response.decode('utf-8', errors='replace')
                if "raw REPL" in text and ("CTRL-B" in text or ">" in text):
                    break
        except BlockingIOError:
            pass
        time.sleep(0.01)
    
    result_text = response.decode('utf-8', errors='replace')
    print(f"Raw REPL response: {result_text.replace(chr(13), '\\\\r').replace(chr(10), '\\\\n')}")
    
    if "raw REPL" in result_text:
        print("✓ Successfully entered Raw REPL using duplex stream approach")
        return True
    else:
        print("✗ Failed to enter Raw REPL")
        return False
    
    proc.terminate()
    proc.wait()

if __name__ == "__main__":
    success = test_duplex_approach()
    print(f"Test {'PASSED' if success else 'FAILED'}")