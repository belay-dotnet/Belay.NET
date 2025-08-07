#!/usr/bin/env python3
"""
Debug script to replicate the C# protocol exactly
"""
import subprocess
import asyncio
import time

async def read_with_timeout(proc, timeout_ms):
    """Replicate C# ReadWithTimeoutAsync logic"""
    buffer = bytearray()
    start_time = time.time()
    timeout_seconds = timeout_ms / 1000.0
    
    while (time.time() - start_time) < timeout_seconds:
        try:
            data = proc.stdout.read(256)
            if data:
                buffer.extend(data)
                text = buffer.decode('utf-8', errors='replace')
                print(f"Buffer now contains: {text.replace(chr(13), '\\r').replace(chr(10), '\\n')}")
                
                # Check completion conditions (same as C#)
                if "raw REPL" in text and ("CTRL-B" in text or ">" in text):
                    print("✓ Found complete Raw REPL entry message")
                    break
                elif "OK" in text and ("\x04" in text or ">" in text):
                    print("✓ Found execution output")
                    break
            else:
                await asyncio.sleep(0.01)
        except:
            await asyncio.sleep(0.01)
    
    return buffer.decode('utf-8', errors='replace')

async def test_exact_csharp_protocol():
    print("Testing exact C# protocol flow...")
    
    proc = subprocess.Popen(
        ["./micropython/ports/unix/build-standard/micropython", "-i"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        bufsize=0
    )
    
    # Step 1: Wait for startup (like C# InitializeAsync)
    print("1. Waiting for startup...")
    await asyncio.sleep(1.0)
    
    # Step 2: Drain startup output
    print("2. Draining startup output...")
    while True:
        try:
            data = proc.stdout.read(1024)
            if data:
                print(f"Drained: {data.decode('utf-8', errors='replace').replace(chr(13), '\\r').replace(chr(10), '\\n')}")
            else:
                break
        except:
            break
    
    # Step 3: Send interrupt (like C#)
    print("3. Sending interrupt...")
    proc.stdin.write(b"\r\x03")
    proc.stdin.flush()
    await asyncio.sleep(0.1)
    
    # Step 4: Drain interrupt response
    print("4. Draining interrupt response...")
    while True:
        try:
            data = proc.stdout.read(1024)
            if data:
                print(f"Interrupt response: {data.decode('utf-8', errors='replace').replace(chr(13), '\\r').replace(chr(10), '\\n')}")
            else:
                break
        except:
            break
    
    # Step 5: Enter Raw REPL (like C# EnterRawModeAsync)
    print("5. Entering Raw REPL...")
    proc.stdin.write(b"\x01")
    proc.stdin.flush()
    
    # Step 6: Read Raw REPL response with timeout
    print("6. Reading Raw REPL response...")
    response = await read_with_timeout(proc, 2000)
    
    if "raw REPL" in response:
        print("✓ Successfully entered Raw REPL")
        
        # Step 7: Try executing code
        print("7. Executing code: 1+2")
        proc.stdin.write(b"1+2\x04")
        proc.stdin.flush()
        
        result = await read_with_timeout(proc, 2000)
        print(f"Execution result: {result.replace(chr(13), '\\r').replace(chr(10), '\\n')}")
        
        if "3" in result:
            print("✓ Code execution successful!")
            return True
    else:
        print(f"✗ Failed to enter Raw REPL. Response: {response}")
    
    proc.terminate()
    proc.wait()
    return False

if __name__ == "__main__":
    result = asyncio.run(test_exact_csharp_protocol())
    print(f"Test {'PASSED' if result else 'FAILED'}")