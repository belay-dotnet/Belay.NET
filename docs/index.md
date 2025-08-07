---
layout: home

hero:
  name: "Belay.NET"
  text: "Control MicroPython devices from .NET"
  tagline: Write C# code that seamlessly executes on microcontrollers, with full IntelliSense, type safety, and async/await support.
  image:
    src: /hero-logo.svg
    alt: Belay.NET
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: View on GitHub
      link: https://github.com/belay-dotnet/belay

features:
  - icon: 🚀
    title: Zero Configuration
    details: Connect and control devices in seconds with automatic device detection and protocol negotiation.
  
  - icon: 🎯
    title: Type-Safe Remote Execution
    details: Full IntelliSense and compile-time safety for remote code with strongly-typed return values.
    
  - icon: ⚡
    title: Async First
    details: Modern async/await patterns throughout the API with proper cancellation token support.
    
  - icon: 🏷️
    title: Attribute-Based Programming
    details: Decorate methods to run seamlessly on devices with [Task], [Setup], [Teardown], and [Thread] attributes.
    
  - icon: 📦
    title: Dependency Injection Ready
    details: First-class DI support with Microsoft.Extensions including configuration, health checks, and factory patterns.
    
  - icon: 🔍
    title: Built-in Monitoring
    details: Health checks, performance metrics, and comprehensive error handling with structured logging.
---

## Quick Example

Transform your .NET application into an IoT powerhouse:

```csharp
// Install: dotnet add package Belay.NET

public class SmartSensor : Device
{
    [Setup]
    public async Task InitializeAsync() =>
        await ExecuteAsync("from machine import Pin, ADC; sensor = ADC(Pin(26))");
        
    [Task(Cache = true, TimeoutMs = 5000)]
    public async Task<float> ReadTemperatureAsync() =>
        await ExecuteAsync<float>("sensor.read_u16() * 3.3 / 65536 * 100");
        
    [Task]
    public async Task<bool> SetLedAsync(bool state) =>
        await ExecuteAsync<bool>($"led.{'on' if state else 'off'}(); True");
}

// Use like any other .NET class
var sensor = new SmartSensor();
await sensor.ConnectAsync("COM3");
await sensor.InitializeAsync();

var temperature = await sensor.ReadTemperatureAsync();
Console.WriteLine($"Current temperature: {temperature}°C");
```

## Why Belay.NET?

::: info From Python to Production
Originally inspired by the Python Belay library, Belay.NET brings the same elegant device integration to the .NET ecosystem with enterprise-grade features like dependency injection, health monitoring, and comprehensive error handling.
:::

### Perfect for IoT Applications

- **Industrial Automation**: Control PLCs and industrial sensors
- **Smart Home**: Build intelligent home automation systems  
- **Research & Education**: Rapid prototyping for academic projects
- **Enterprise IoT**: Scalable device management with ASP.NET Core

### Supported Hardware

<div class="hardware-grid">
  <div class="hardware-card">
    <h4>🔌 Raspberry Pi Pico</h4>
    <p>Full MicroPython and CircuitPython support</p>
    <span class="status supported">Fully Supported</span>
  </div>
  
  <div class="hardware-card">
    <h4>📡 ESP32</h4>
    <p>WiFi-enabled microcontroller with rich peripherals</p>
    <span class="status supported">Fully Supported</span>
  </div>
  
  <div class="hardware-card">
    <h4>⚡ PyBoard</h4>
    <p>Original MicroPython development board</p>
    <span class="status supported">Fully Supported</span>
  </div>
  
  <div class="hardware-card">
    <h4>🐍 CircuitPython</h4>
    <p>Adafruit's education-focused Python variant</p>
    <span class="status beta">Beta Support</span>
  </div>
</div>

[View full compatibility matrix →](/hardware/compatibility)

## What's New in v0.2.0

- ✨ **Dependency Injection**: First-class Microsoft.Extensions.DependencyInjection support
- 🏥 **Health Checks**: Built-in system and device health monitoring
- ⚙️ **Configuration**: Comprehensive configuration management with validation
- 🏭 **Factory Patterns**: Clean abstractions for device and communicator creation
- 🔧 **Session Management**: Advanced session lifecycle and resource management
- ❌ **Exception Handling**: Unified exception hierarchy with device error mapping

[Read the full changelog →](/changelog)

<style>
.hardware-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1rem;
  margin: 2rem 0;
}

.hardware-card {
  border: 1px solid var(--vp-c-border);
  border-radius: 8px;
  padding: 1.5rem;
  background: var(--vp-c-bg-soft);
}

.hardware-card h4 {
  margin: 0 0 0.5rem 0;
  color: var(--vp-c-text-1);
}

.hardware-card p {
  margin: 0 0 1rem 0;
  color: var(--vp-c-text-2);
  font-size: 0.9rem;
}

.status {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
}

.status.supported {
  background: var(--vp-c-green-soft);
  color: var(--vp-c-green-darker);
}

.status.beta {
  background: var(--vp-c-yellow-soft);
  color: var(--vp-c-yellow-darker);
}
</style>