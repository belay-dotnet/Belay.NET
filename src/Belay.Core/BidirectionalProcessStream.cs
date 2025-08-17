// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

/// <summary>
/// A bidirectional stream wrapper that combines process input and output streams.
/// Required for subprocess connections where we need both read and write capabilities.
/// </summary>
internal class BidirectionalProcessStream : Stream {
    private readonly Stream inputStream;
    private readonly Stream outputStream;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BidirectionalProcessStream"/> class.
    /// </summary>
    /// <param name="inputStream">The stream for writing to the process (StandardInput).</param>
    /// <param name="outputStream">The stream for reading from the process (StandardOutput).</param>
    public BidirectionalProcessStream(Stream inputStream, Stream outputStream) {
        this.inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        this.outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
    }

    /// <inheritdoc/>
    public override bool CanRead => outputStream.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => inputStream.CanWrite;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush() {
        inputStream.Flush();
    }

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken) {
        await inputStream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) {
        return outputStream.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        return await outputStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        return await outputStream.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) {
        inputStream.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        await inputStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        await inputStream.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        if (!disposed && disposing) {
            inputStream?.Dispose();
            outputStream?.Dispose();
            disposed = true;
        }

        base.Dispose(disposing);
    }
}
