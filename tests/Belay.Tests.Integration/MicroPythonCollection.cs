// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
using System;
using Belay.Core.Testing;
using Xunit;

/// <summary>
/// Collection fixture for MicroPython tests to ensure the unix port is built once
/// </summary>
public class MicroPythonFixture : IDisposable {
    public string MicroPythonPath { get; }

    public MicroPythonFixture() {
        // Build MicroPython unix port if needed
        this.MicroPythonPath = MicroPythonUnixPort.FindMicroPythonExecutable()
            ?? MicroPythonUnixPort.BuildUnixPort();

        if (string.IsNullOrEmpty(this.MicroPythonPath)) {
            throw new InvalidOperationException(
                "Failed to build or find MicroPython unix port. " +
                "Please ensure the micropython submodule is initialized and build dependencies are installed.");
        }
    }

    public void Dispose() {
        // Cleanup if needed
    }
}

[CollectionDefinition("MicroPython")]
public class MicroPythonCollection : ICollectionFixture<MicroPythonFixture> {
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
