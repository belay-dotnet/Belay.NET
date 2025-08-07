#!/usr/bin/env python3
"""
Test subprocess communication with C# style protocol
"""
import subprocess
import asyncio
import time

async def test_csharp_style():
    print("Testing C# style Raw REPL communication...")
    
    proc = subprocess.Popen(
        ["./micropython/ports/unix/build-standard/micropython", "-i"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        bufsize=0
    )
    
    # Give initial startup time
    await asyncio.sleep(0.2)
    
    # Drain startup output
    print("Draining startup output...")
    while True:
        try:
            # Non-blocking read
            output = proc.stdout.read(1024)
            if output:
                print(f"Drained: {output.decode('utf-8', errors='replace').replace(chr(13), '\\r').replace(chr(10), '\\n')}")
            else:
                break
        except:
            break
    
    # Send interrupt to clear state (like C# does)
    print("Sending interrupt...")
    proc.stdin.write(b"\r\x03")
    proc.stdin.flush()
    await asyncio.sleep(0.1)
    
    # Drain interrupt response
    while True:
        try:
            output = proc.stdout.read(1024)
            if output:
                print(f"Interrupt response: {output.decode('utf-8', errors='replace').replace(chr(13), '\\r').replace(chr(10), '\\n')}")
            else:
                break
        except:
            break
    
    # Send Ctrl-A to enter raw mode
    print("Entering Raw REPL...")
    proc.stdin.write(b"\x01")
    proc.stdin.flush()
    
    # Read with timeout (like C# ReadWithTimeoutAsync)
    start_time = time.time()
    response = b""
    while time.time() - start_time < 1.0:
        try:
            byte_data = proc.stdout.read(256)
            if byte_data:
                response += byte_data
                text = response.decode('utf-8', errors='replace')
                if "raw REPL" in text:
                    print(f"Got Raw REPL response: {text.replace(chr(13), '\\r').replace(chr(10), '\\n')}")
                    break
        except:
            pass
        await asyncio.sleep(0.01)
    
    if b"raw REPL" not in response:
        print(f"Failed to get Raw REPL. Got: {response.decode('utf-8', errors='replace')}")
        proc.terminate()
        return False
    
    # Test code execution
    print("Testing code execution: '1+1'")
    proc.stdin.write(b"1+1\x04")  # \x04 = Ctrl-D to execute
    proc.stdin.flush()
    
    # Read execution result
    await asyncio.sleep(0.2)
    result = b""
    try:
        result = proc.stdout.read(1024)
        result_text = result.decode('utf-8', errors='replace').replace(chr(13), '\\r').replace(chr(10), '\\n')
        print(f"Execution result: {result_text}")
        
        if "OK" in result_text and "2" in result_text:
            print("✓ Code execution successful!")
            success = True
        else:
            print("✗ Code execution failed")
            success = False
    except Exception as e:
        print(f"Error reading result: {e}")
        success = False
    
    proc.terminate()
    proc.wait()
    return success

if __name__ == "__main__":
    result = asyncio.run(test_csharp_style())
    print(f"Test {'PASSED' if result else 'FAILED'}")