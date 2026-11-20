namespace YARG.Core.UnitTests.IO;

/// <summary>
/// Returns at most <c>maxPerRead</c> bytes from each Read call, as network streams may.
/// </summary>
public sealed class ShortReadStream : Stream
{
    private readonly Stream _inner;
    private readonly int _maxPerRead;

    public ShortReadStream(Stream inner, int maxPerRead)
    {
        _inner = inner;
        _maxPerRead = maxPerRead;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _inner.Read(buffer, offset, Math.Min(count, _maxPerRead));
    }

    public override int Read(Span<byte> buffer)
    {
        return _inner.Read(buffer[..Math.Min(buffer.Length, _maxPerRead)]);
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void Flush() { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
