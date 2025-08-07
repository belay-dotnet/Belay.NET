#!/usr/bin/env python3
"""
Test if MicroPython unix port with -i flag works with Raw REPL
"""
import subprocess
import time
import threading

def main():
    print("Testing MicroPython with -i flag...")
    
    proc = subprocess.Popen(
        ["/home/corona/belay.net/micropython/ports/unix/build-standard/micropython", "-i"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0
    )
    
    output_lines = []
    
    def read_output():
        try:
            for line in proc.stdout:
                output_lines.append(f"[OUT] {line.rstrip()}")
                print(f"[OUT] {line.rstrip()}")
                if len(output_lines) > 10:  # Limit output
                    break
        except Exception as e:
            print(f"Output error: {e}")
    
    # Start output reader
    output_thread = threading.Thread(target=read_output, daemon=True)
    output_thread.start()
    
    # Give it time to show startup
    time.sleep(2)
    
    print("Sending \\r\\x03 (interrupt)...")
    proc.stdin.write("\r\x03")
    proc.stdin.flush()
    
    time.sleep(1)
    
    print("Sending \\r\\x01 (enter raw REPL)...")  
    proc.stdin.write("\r\x01")
    proc.stdin.flush()
    
    time.sleep(2)
    
    print("Terminating...")
    proc.terminate()
    try:
        proc.wait(timeout=3)
    except:
        proc.kill()
        
    print(f"Total output lines: {len(output_lines)}")

if __name__ == "__main__":
    main()