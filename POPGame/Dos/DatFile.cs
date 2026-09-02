namespace POPGame.Dos;

/// <summary>
/// Reader for Prince of Persia DOS resource containers (*.DAT).
///
/// Layout (verified against E:\DOS\games\Prince\*.DAT):
///   0x00  uint32  indexOffset
///   0x04  uint16  indexSize          (indexOffset + indexSize == file length)
///   ...   resource payloads, each preceded by a single checksum byte
///   idx   uint16  entryCount
///         entryCount * { uint16 id; uint32 offset; uint16 size; }
/// </summary>
public sealed class DatFile
{
    public readonly record struct Entry(ushort Id, uint Offset, ushort Size);

    private readonly byte[] _bytes;
    private readonly Dictionary<ushort, Entry> _byId = new();

    public string Path { get; }
    public IReadOnlyList<Entry> Entries { get; }

    public DatFile(string path)
    {
        Path = path;
        _bytes = File.ReadAllBytes(path);

        uint indexOffset = BitConverter.ToUInt32(_bytes, 0);
        ushort indexSize = BitConverter.ToUInt16(_bytes, 4);
        if (indexOffset + indexSize != _bytes.Length)
            throw new InvalidDataException(
                $"{path}: index {indexOffset}+{indexSize} != file length {_bytes.Length}");

        int p = (int)indexOffset;
        ushort count = BitConverter.ToUInt16(_bytes, p);
        p += 2;

        var list = new List<Entry>(count);
        for (int i = 0; i < count; i++, p += 8)
        {
            var e = new Entry(
                BitConverter.ToUInt16(_bytes, p),
                BitConverter.ToUInt32(_bytes, p + 2),
                BitConverter.ToUInt16(_bytes, p + 6));
            list.Add(e);
            _byId[e.Id] = e;
        }
        Entries = list;
    }

    public bool Has(int id) => _byId.ContainsKey((ushort)id);

    /// <summary>Payload of a resource, with the leading checksum byte stripped.</summary>
    public ReadOnlySpan<byte> Resource(int id)
    {
        if (!_byId.TryGetValue((ushort)id, out var e))
            throw new KeyNotFoundException($"{Path}: no resource id {id}");
        return _bytes.AsSpan((int)e.Offset + 1, e.Size);
    }

    public ReadOnlySpan<byte> ResourceOrEmpty(int id) =>
        _byId.TryGetValue((ushort)id, out var e)
            ? _bytes.AsSpan((int)e.Offset + 1, e.Size)
            : ReadOnlySpan<byte>.Empty;
}
