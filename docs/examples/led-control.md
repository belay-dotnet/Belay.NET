# LED Control

Learn GPIO control and hardware interaction by building LED control applications with Belay.NET. This tutorial covers basic digital output, PWM control, and the attribute-based programming model.

## Prerequisites

- MicroPython device with a built-in LED or external LED connected
- Basic understanding of GPIO pins
- Completed [First Connection](./first-connection.md) tutorial

## Hardware Setup

### Built-in LED (Recommended for beginners)
Most development boards have a built-in LED:
- **Raspberry Pi Pico**: Pin 25
- **ESP32**: Usually Pin 2 (varies by board)
- **PyBoard**: Pin 4 (red LED)

### External LED
If using an external LED:
1. Connect LED anode (+) to GPIO pin through a 220Ω resistor
2. Connect LED cathode (-) to ground (GND)
3. Use any available GPIO pin (we'll use Pin 16 in examples)

## Basic LED Control

Let's start with simple on/off control:

```csharp
using Belay.Core;
using System;

try
{
    using var device = await Device.ConnectAsync("COM3");
    
    Console.WriteLine("Setting up LED...");
    
    // Initialize LED on Pin 25 (Raspberry Pi Pico built-in LED)
    await device.ExecuteAsync("""
        from machine import Pin
        led = Pin(25, Pin.OUT)  # Change pin number for your board
        led.off()  # Start with LED off
        print("LED initialized on Pin 25")
        """);
    
    // Turn LED on
    Console.WriteLine("Turning LED on...");
    await device.ExecuteAsync("led.on()");
    await Task.Delay(2000);
    
    // Turn LED off
    Console.WriteLine("Turning LED off...");
    await device.ExecuteAsync("led.off()");
    await Task.Delay(1000);
    
    // Blink pattern
    Console.WriteLine("Blinking LED 5 times...");
    await device.ExecuteAsync("""
        import time
        for i in range(5):
            led.on()
            time.sleep(0.2)
            led.off()
            time.sleep(0.2)
        """);
    
    Console.WriteLine("LED control demo complete!");
}
catch (DeviceConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
catch (DeviceExecutionException ex)
{
    Console.WriteLine($"Device error: {ex.Message}");
}
```

## Attribute-Based LED Control

Now let's create a proper LED controller class using Belay.NET's attribute system:

```csharp
using Belay.Core;
using Belay.Attributes;
using System;

public class LedController : Device
{
    private readonly int _pinNumber;
    
    public LedController(int pinNumber = 25)
    {
        _pinNumber = pinNumber;
    }
    
    [Setup]
    public async Task InitializeAsync()
    {
        await ExecuteAsync($"""
            from machine import Pin
            import time
            
            led = Pin({_pinNumber}, Pin.OUT)
            led.off()
            print(f"LED initialized on Pin {_pinNumber}")
            """);
    }
    
    [Task]
    public async Task TurnOnAsync()
    {
        await ExecuteAsync("led.on()");
    }
    
    [Task]
    public async Task TurnOffAsync()
    {
        await ExecuteAsync("led.off()");
    }
    
    [Task]
    public async Task ToggleAsync()
    {
        await ExecuteAsync("led.toggle()");
    }
    
    [Task]
    public async Task<bool> IsOnAsync()
    {
        return await ExecuteAsync<bool>("bool(led.value())");
    }
    
    [Task]
    public async Task BlinkAsync(int times = 3, int delayMs = 500)
    {
        await ExecuteAsync($"""
            for i in range({times}):
                led.on()
                time.sleep_ms({delayMs})
                led.off()
                time.sleep_ms({delayMs})
            """);
    }
    
    [Task]
    public async Task BlinkPatternAsync(int[] pattern)
    {
        await ExecuteAsync($"""
            pattern = {string.Join(",", pattern)}
            for delay_ms in pattern:
                led.toggle()
                time.sleep_ms(delay_ms)
            """);
    }
    
    [Teardown]
    public async Task CleanupAsync()
    {
        await ExecuteAsync("""
            led.off()
            print("LED turned off during cleanup")
            """);
    }
}

// Usage
class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Create LED controller (Pin 25 for Raspberry Pi Pico)
            var led = new LedController(pinNumber: 25);
            await led.ConnectAsync("COM3");
            
            Console.WriteLine("LED Controller Demo");
            
            // Basic control
            await led.TurnOnAsync();
            Console.WriteLine($"LED is on: {await led.IsOnAsync()}");
            await Task.Delay(1000);
            
            await led.TurnOffAsync();
            Console.WriteLine($"LED is on: {await led.IsOnAsync()}");
            await Task.Delay(1000);
            
            // Blinking
            Console.WriteLine("Blinking 3 times...");
            await led.BlinkAsync(times: 3, delayMs: 300);
            
            // Custom pattern (on 100ms, off 200ms, on 500ms, off 100ms)
            Console.WriteLine("Custom blink pattern...");
            await led.BlinkPatternAsync(new[] { 100, 200, 500, 100 });
            
            // Cleanup happens automatically when disposed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```

## PWM LED Control (Dimming)

For boards that support PWM, you can control LED brightness:

```csharp
using Belay.Core;
using Belay.Attributes;
using System;

public class DimmableLed : Device
{
    private readonly int _pinNumber;
    
    public DimmableLed(int pinNumber = 25)
    {
        _pinNumber = pinNumber;
    }
    
    [Setup]
    public async Task InitializeAsync()
    {
        await ExecuteAsync($"""
            from machine import Pin, PWM
            import time
            
            led_pwm = PWM(Pin({_pinNumber}))
            led_pwm.freq(1000)  # 1 kHz frequency
            led_pwm.duty_u16(0)  # Start with LED off
            print(f"PWM LED initialized on Pin {_pinNumber}")
            """);
    }
    
    [Task]
    public async Task SetBrightnessAsync(float brightness)
    {
        if (brightness < 0 || brightness > 1)
            throw new ArgumentException("Brightness must be between 0 and 1");
            
        var duty = (int)(brightness * 65535); // Convert to 16-bit duty cycle
        await ExecuteAsync($"led_pwm.duty_u16({duty})");
    }
    
    [Task]
    public async Task SetBrightnessPercentAsync(int percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentException("Percent must be between 0 and 100");
            
        var duty = (int)(percent / 100.0 * 65535);
        await ExecuteAsync($"led_pwm.duty_u16({duty})");
    }
    
    [Task]
    public async Task FadeInAsync(int durationMs = 2000)
    {
        await ExecuteAsync($"""
            steps = 50
            delay_ms = {durationMs} // steps
            
            for i in range(steps + 1):
                brightness = i / steps
                duty = int(brightness * 65535)
                led_pwm.duty_u16(duty)
                time.sleep_ms(delay_ms)
            """);
    }
    
    [Task]
    public async Task FadeOutAsync(int durationMs = 2000)
    {
        await ExecuteAsync($"""
            steps = 50
            delay_ms = {durationMs} // steps
            
            for i in range(steps, -1, -1):
                brightness = i / steps
                duty = int(brightness * 65535)
                led_pwm.duty_u16(duty)
                time.sleep_ms(delay_ms)
            """);
    }
    
    [Task]
    public async Task PulseAsync(int cycles = 3, int durationMs = 2000)
    {
        for (int i = 0; i < cycles; i++)
        {
            await FadeInAsync(durationMs / 2);
            await FadeOutAsync(durationMs / 2);
        }
    }
    
    [Task]
    public async Task<float> GetBrightnessAsync()
    {
        var duty = await ExecuteAsync<int>("led_pwm.duty_u16()");
        return duty / 65535.0f;
    }
    
    [Teardown]
    public async Task CleanupAsync()
    {
        await ExecuteAsync("""
            led_pwm.duty_u16(0)
            led_pwm.deinit()
            print("PWM LED cleaned up")
            """);
    }
}

// Usage
class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var led = new DimmableLed(pinNumber: 25);
            await led.ConnectAsync("COM3");
            
            Console.WriteLine("PWM LED Demo");
            
            // Set different brightness levels
            Console.WriteLine("Testing brightness levels...");
            for (int percent = 0; percent <= 100; percent += 25)
            {
                Console.WriteLine($"Setting brightness to {percent}%");
                await led.SetBrightnessPercentAsync(percent);
                await Task.Delay(1000);
            }
            
            // Fade effects
            Console.WriteLine("Fade in...");
            await led.FadeInAsync(2000);
            
            Console.WriteLine("Fade out...");
            await led.FadeOutAsync(2000);
            
            // Pulsing effect
            Console.WriteLine("Pulsing 3 times...");
            await led.PulseAsync(cycles: 3, durationMs: 1500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```

## Multi-LED Control

Control multiple LEDs simultaneously:

```csharp
using Belay.Core;
using Belay.Attributes;
using System;

public class MultiLedController : Device
{
    private readonly int[] _pinNumbers;
    
    public MultiLedController(params int[] pinNumbers)
    {
        _pinNumbers = pinNumbers ?? throw new ArgumentNullException(nameof(pinNumbers));
        if (_pinNumbers.Length == 0)
            throw new ArgumentException("At least one pin number must be specified");
    }
    
    [Setup]
    public async Task InitializeAsync()
    {
        var pins = string.Join(",", _pinNumbers);
        await ExecuteAsync($"""
            from machine import Pin
            import time
            
            led_pins = [{pins}]
            leds = [Pin(pin, Pin.OUT) for pin in led_pins]
            
            # Turn all LEDs off initially
            for led in leds:
                led.off()
                
            print(f"Initialized {len(leds)} LEDs on pins {led_pins}")
            """);
    }
    
    [Task]
    public async Task TurnOnAllAsync()
    {
        await ExecuteAsync("for led in leds: led.on()");
    }
    
    [Task]
    public async Task TurnOffAllAsync()
    {
        await ExecuteAsync("for led in leds: led.off()");
    }
    
    [Task]
    public async Task SetLedAsync(int index, bool state)
    {
        if (index < 0 || index >= _pinNumbers.Length)
            throw new ArgumentException($"LED index must be between 0 and {_pinNumbers.Length - 1}");
            
        var method = state ? "on" : "off";
        await ExecuteAsync($"leds[{index}].{method}()");
    }
    
    [Task]
    public async Task SetPatternAsync(bool[] pattern)
    {
        if (pattern.Length != _pinNumbers.Length)
            throw new ArgumentException($"Pattern must have exactly {_pinNumbers.Length} values");
            
        var boolPattern = string.Join(",", pattern.Select(b => b ? "True" : "False"));
        await ExecuteAsync($"""
            pattern = [{boolPattern}]
            for i, state in enumerate(pattern):
                if state:
                    leds[i].on()
                else:
                    leds[i].off()
            """);
    }
    
    [Task]
    public async Task ChaseAsync(int delayMs = 200, int cycles = 3)
    {
        await ExecuteAsync($"""
            for cycle in range({cycles}):
                # Forward chase
                for i in range(len(leds)):
                    for led in leds:
                        led.off()
                    leds[i].on()
                    time.sleep_ms({delayMs})
                    
                # Backward chase
                for i in range(len(leds) - 1, -1, -1):
                    for led in leds:
                        led.off()
                    leds[i].on()
                    time.sleep_ms({delayMs})
                    
            # Turn all off at the end
            for led in leds:
                led.off()
            """);
    }
    
    [Task]
    public async Task BlinkSequenceAsync(int delayMs = 300)
    {
        await ExecuteAsync($"""
            # Blink each LED in sequence
            for led in leds:
                led.on()
                time.sleep_ms({delayMs})
                led.off()
                time.sleep_ms({delayMs})
            """);
    }
    
    [Task]
    public async Task<bool[]> GetStatesAsync()
    {
        var states = await ExecuteAsync<List<bool>>("[bool(led.value()) for led in leds]");
        return states.ToArray();
    }
    
    [Teardown]
    public async Task CleanupAsync()
    {
        await ExecuteAsync("""
            for led in leds:
                led.off()
            print("All LEDs turned off during cleanup")
            """);
    }
}

// Usage
class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Create controller for 4 LEDs on pins 16, 17, 18, 19
            var leds = new MultiLedController(16, 17, 18, 19);
            await leds.ConnectAsync("COM3");
            
            Console.WriteLine("Multi-LED Demo");
            
            // Turn all on/off
            Console.WriteLine("All LEDs on...");
            await leds.TurnOnAllAsync();
            await Task.Delay(1000);
            
            Console.WriteLine("All LEDs off...");
            await leds.TurnOffAllAsync();
            await Task.Delay(1000);
            
            // Individual control
            Console.WriteLine("Testing individual LEDs...");
            for (int i = 0; i < 4; i++)
            {
                Console.WriteLine($"LED {i} on");
                await leds.SetLedAsync(i, true);
                await Task.Delay(500);
                await leds.SetLedAsync(i, false);
            }
            
            // Pattern display
            Console.WriteLine("Pattern: alternating...");
            await leds.SetPatternAsync(new[] { true, false, true, false });
            await Task.Delay(1000);
            
            await leds.SetPatternAsync(new[] { false, true, false, true });
            await Task.Delay(1000);
            
            // Chase effect
            Console.WriteLine("Chase effect...");
            await leds.ChaseAsync(delayMs: 150, cycles: 2);
            
            // Sequence blink
            Console.WriteLine("Sequence blink...");
            await leds.BlinkSequenceAsync(delayMs: 200);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```

## Interactive LED Controller

Create an interactive console application:

```csharp
using Belay.Core;
using System;

class InteractiveLedController
{
    private readonly LedController _led;
    
    public InteractiveLedController(LedController led)
    {
        _led = led;
    }
    
    public async Task RunAsync()
    {
        Console.WriteLine("=== Interactive LED Controller ===");
        Console.WriteLine("Commands:");
        Console.WriteLine("  on       - Turn LED on");
        Console.WriteLine("  off      - Turn LED off");
        Console.WriteLine("  toggle   - Toggle LED state");
        Console.WriteLine("  status   - Show LED status");
        Console.WriteLine("  blink    - Blink LED (3 times)");
        Console.WriteLine("  blink N  - Blink LED N times");
        Console.WriteLine("  pattern  - Custom blink pattern");
        Console.WriteLine("  quit     - Exit");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("LED> ");
            var input = Console.ReadLine()?.Trim().ToLower();
            
            if (string.IsNullOrEmpty(input))
                continue;
                
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0];
            
            try
            {
                switch (command)
                {
                    case "on":
                        await _led.TurnOnAsync();
                        Console.WriteLine("LED turned on");
                        break;
                        
                    case "off":
                        await _led.TurnOffAsync();
                        Console.WriteLine("LED turned off");
                        break;
                        
                    case "toggle":
                        await _led.ToggleAsync();
                        Console.WriteLine("LED toggled");
                        break;
                        
                    case "status":
                        var isOn = await _led.IsOnAsync();
                        Console.WriteLine($"LED is {(isOn ? "ON" : "OFF")}");
                        break;
                        
                    case "blink":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var times))
                        {
                            await _led.BlinkAsync(times);
                            Console.WriteLine($"Blinked {times} times");
                        }
                        else
                        {
                            await _led.BlinkAsync();
                            Console.WriteLine("Blinked 3 times");
                        }
                        break;
                        
                    case "pattern":
                        Console.Write("Enter pattern (comma-separated delays in ms): ");
                        var patternInput = Console.ReadLine();
                        if (!string.IsNullOrEmpty(patternInput))
                        {
                            var pattern = patternInput.Split(',')
                                .Select(s => s.Trim())
                                .Where(s => int.TryParse(s, out _))
                                .Select(int.Parse)
                                .ToArray();
                                
                            if (pattern.Length > 0)
                            {
                                await _led.BlinkPatternAsync(pattern);
                                Console.WriteLine("Pattern executed");
                            }
                            else
                            {
                                Console.WriteLine("Invalid pattern");
                            }
                        }
                        break;
                        
                    case "quit":
                        return;
                        
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var led = new LedController(pinNumber: 25);  // Adjust pin for your board
            await led.ConnectAsync("COM3");             // Adjust port for your device
            
            var controller = new InteractiveLedController(led);
            await controller.RunAsync();
            
            Console.WriteLine("Goodbye!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start: {ex.Message}");
        }
    }
}
```

## Common Issues and Solutions

### LED Not Responding
1. **Check pin number**: Verify you're using the correct pin for your board
2. **Check wiring**: For external LEDs, ensure proper connections and resistor
3. **Power supply**: Some high-power LEDs may need external power

### GPIO Already in Use
```csharp
// Reset GPIO state if needed
await device.ExecuteAsync("led.deinit()");  // Deinitialize first
await device.ExecuteAsync("led = Pin(25, Pin.OUT)");  // Reinitialize
```

### PWM Not Available
Some pins don't support PWM. Try a different pin or check your board's documentation.

## Board-Specific Pin Configurations

```csharp
public static class BoardPins
{
    public static class RaspberryPiPico
    {
        public const int BuiltInLed = 25;
        public static readonly int[] GpioPins = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 };
    }
    
    public static class ESP32DevKit
    {
        public const int BuiltInLed = 2;  // Common, but varies by board
        public static readonly int[] GpioPins = { 4, 5, 12, 13, 14, 15, 16, 17, 18, 19, 21, 22, 23, 25, 26, 27, 32, 33 };
    }
    
    public static class PyBoard
    {
        public const int RedLed = 4;
        public const int GreenLed = 3;
        public const int YellowLed = 2;
        public const int BlueLed = 1;
    }
}

// Usage with board-specific pins
var led = new LedController(BoardPins.RaspberryPiPico.BuiltInLed);
```

## Next Steps

Now that you can control LEDs, try these tutorials:

1. **[Sensor Reading](./sensor-reading.md)** - Read analog sensors and combine with LED indicators
2. **[Multiple Devices](./multiple-devices.md)** - Control LEDs on multiple devices
3. **[ASP.NET Core Integration](./aspnet-core.md)** - Control LEDs from a web API
4. **[Background Services](./background-services.md)** - Create LED monitoring services

The LED control patterns you've learned here form the foundation for more complex hardware interactions!