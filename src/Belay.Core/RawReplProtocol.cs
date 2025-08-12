using System.Text;

namespace Belay.Core;

/// <summary>
/// Simple, documented Raw REPL protocol implementation.
/// Replaces complex protocol abstractions with direct, understandable code.
/// See ICD-001 for protocol specification.
/// </summary>
public static class RawReplProtocol
{
    // Protocol control characters (ICD-001)
    public const byte ENTER_RAW = 0x01;
    public const byte EXIT_RAW = 0x02;
    public const byte INTERRUPT = 0x03;
    public const byte EXECUTE = 0x04;
    public const byte RAW_PASTE = 0x05;

    // Protocol responses
    private const string RAW_PROMPT = "raw REPL; CTRL-B to exit\r\n>";
    private const string NORMAL_PROMPT = ">";

    /// <summary>
    /// Executes Python code on a MicroPython device using Raw REPL protocol.
    /// </summary>
    /// <param name="stream">The communication stream to the device.</param>
    /// <param name="pythonCode">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The output from the device.</returns>
    /// <exception cref="DeviceException">Thrown when communication or execution fails.</exception>
    public static async Task<string> ExecuteCode(Stream stream, string pythonCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Enter raw mode
            await stream.WriteAsync(new byte[] { ENTER_RAW }, cancellationToken);
            await WaitForPrompt(stream, NORMAL_PROMPT, cancellationToken);

            // Send code
            var codeBytes = Encoding.UTF8.GetBytes(pythonCode);
            await stream.WriteAsync(codeBytes, cancellationToken);

            // Execute
            await stream.WriteAsync(new byte[] { EXECUTE }, cancellationToken);

            // Read result
            var result = await ReadUntilPrompt(stream, cancellationToken);

            // Exit raw mode
            await stream.WriteAsync(new byte[] { EXIT_RAW }, cancellationToken);

            return result;
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Raw REPL execution failed: {ex.Message}", ex)
            {
                ExecutedCode = pythonCode
            };
        }
    }

    /// <summary>
    /// Executes Python code with large transfer support using Raw-Paste mode.
    /// </summary>
    /// <param name="stream">The communication stream to the device.</param>
    /// <param name="pythonCode">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The output from the device.</returns>
    public static async Task<string> ExecuteCodeWithPaste(Stream stream, string pythonCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Enter raw mode
            await stream.WriteAsync(new byte[] { ENTER_RAW }, cancellationToken);
            await WaitForPrompt(stream, NORMAL_PROMPT, cancellationToken);

            // Enter raw-paste mode
            await stream.WriteAsync(new byte[] { RAW_PASTE, 0x01 }, cancellationToken);
            
            // Read window size (2 bytes)
            var windowBuffer = new byte[2];
            await stream.ReadExactlyAsync(windowBuffer, cancellationToken);
            var windowSize = (windowBuffer[0] << 8) | windowBuffer[1];

            // Send code in chunks with flow control
            var codeBytes = Encoding.UTF8.GetBytes(pythonCode);
            await SendWithFlowControl(stream, codeBytes, windowSize, cancellationToken);

            // Execute
            await stream.WriteAsync(new byte[] { EXECUTE }, cancellationToken);

            // Read result
            var result = await ReadUntilPrompt(stream, cancellationToken);

            return result;
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Raw-Paste execution failed: {ex.Message}", ex)
            {
                ExecutedCode = pythonCode
            };
        }
    }

    private static async Task WaitForPrompt(Stream stream, string expectedPrompt, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var received = new StringBuilder();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                throw new DeviceException("Device disconnected while waiting for prompt");

            received.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            
            if (received.ToString().Contains(expectedPrompt))
                return;
        }
        
        throw new OperationCanceledException("Timeout waiting for device prompt");
    }

    private static async Task<string> ReadUntilPrompt(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var result = new StringBuilder();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                break;

            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            result.Append(text);
            
            // Check for prompt indicating end of output
            if (text.Contains("\x04>"))
                break;
        }
        
        var output = result.ToString();
        
        // Clean up the output (remove control characters and prompts)
        var startIndex = output.IndexOf("OK\x04\x04>");
        if (startIndex >= 0)
        {
            output = output[(startIndex + 5)..];
        }
        
        var endIndex = output.LastIndexOf("\x04>");
        if (endIndex >= 0)
        {
            output = output[..endIndex];
        }
        
        return output.Trim();
    }

    private static async Task SendWithFlowControl(Stream stream, byte[] data, int windowSize, CancellationToken cancellationToken)
    {
        var offset = 0;
        
        while (offset < data.Length)
        {
            var chunkSize = Math.Min(windowSize, data.Length - offset);
            await stream.WriteAsync(data.AsMemory(offset, chunkSize), cancellationToken);
            offset += chunkSize;
            
            if (offset < data.Length)
            {
                // Wait for flow control signal
                var flowBuffer = new byte[1];
                await stream.ReadExactlyAsync(flowBuffer, cancellationToken);
                
                if (flowBuffer[0] != 0x01)
                    throw new DeviceException($"Unexpected flow control byte: 0x{flowBuffer[0]:X2}");
            }
        }
    }
}