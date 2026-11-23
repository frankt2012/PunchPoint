using System.Buffers.Binary;
using System.Text;

namespace PunchPoint.Core.Jet;

/// <summary>
/// Minimal read-only reader for Microsoft Access "Jet 4" databases
/// (Access 2000/2002/2003 .mdb), which is the format timeQplus v3 stores its
/// data in (arx_active.mdb).
///
/// It intentionally has no dependency on the Microsoft Access Database Engine
/// (JET/ACE). Those engines are 32-bit only or require an Office install, and their
/// absence is one of the main reasons the original software fails on Windows 11.
///
/// The implementation follows the publicly documented Jet 4 page layout, with
/// reference to the Apache-2.0 licensed Jackcess project for byte offsets.
/// See NOTICE.md.
/// </summary>
public sealed partial class Jet4Database : IDisposable
{
    public const int PageSize = 4096;

    private readonly byte[] _data;
    private readonly Dictionary<string, Jet4Table> _tableCache = new(StringComparer.OrdinalIgnoreCase);
    private Jet4Table? _catalog;

    private Jet4Database(byte[] data, string path)
    {
        _data = data;
        FilePath = path;
    }

    public string FilePath { get; }

    public IReadOnlyList<string> Warnings { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Opens and validates a Jet 4 database file. Throws <see cref="Jet4FormatException"/>
    /// when the file is not a supported Jet 4 database.
    /// </summary>
    public static Jet4Database Open(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new Jet4FormatException($"Database file not found: {path}");
        if (info.Length < PageSize)
            throw new Jet4FormatException($"'{path}' is too small to be an Access database.");
        if (info.Length > int.MaxValue)
            throw new Jet4FormatException($"'{path}' is larger than 2 GB and cannot be read in memory.");

        byte[] data = File.ReadAllBytes(path);

        // Jet 4 magic: 00 01 00 00 "Standard Jet DB" at offset 0.
        if (data[0] != 0x00 || data[4] != (byte)'S' || data[5] != (byte)'t')
            throw new Jet4FormatException($"'{path}' is not a Microsoft Access database.");

        int version = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0x14, 4));
        if (version != 1)
        {
            throw new Jet4FormatException(version switch
            {
                0 => "This is an Access 97 (Jet 3) database. Only Access 2000-2003 (Jet 4) files are supported. " +
                     "Open it in Access and save it as an Access 2002-2003 database, then try again.",
                _ => $"Unsupported Access database version (header code {version}). Only Access 2000-2003 databases are supported."
            });
        }

        return new Jet4Database(data, path);
    }

    public void Dispose() { }

    // ---------------------------------------------------------------------
    // Page access
    // ---------------------------------------------------------------------

    internal ReadOnlySpan<byte> Page(int pageNumber)
    {
        long offset = (long)pageNumber * PageSize;
        if (pageNumber < 0 || offset + PageSize > _data.Length)
            throw new Jet4FormatException($"Page {pageNumber} is outside the database file (corrupt file?).");
        return _data.AsSpan((int)offset, PageSize);
    }

    public int PageCount => _data.Length / PageSize;

    internal byte PageType(int pageNumber) => Page(pageNumber)[0];

    // ---------------------------------------------------------------------
    // Catalog
    // ---------------------------------------------------------------------

    private Jet4Table Catalog => _catalog ??= LoadTable(2, "MSysObjects");

    /// <summary>Names of the user (non-system) tables in the database.</summary>
    public IReadOnlyList<string> TableNames
    {
        get
        {
            var names = new List<string>();
            foreach (var row in Catalog.Rows())
            {
                string? name = row.GetString("Name");
                if (name is null) continue;
                if (name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)) continue;
                if (!row.TryGetInt32("Type", out int type) || type != 1) continue;
                names.Add(name);
            }
            return names;
        }
    }

    public bool HasTable(string name) => TableNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the table with the given case-insensitive name.</summary>
    public Jet4Table GetTable(string name)
    {
        if (_tableCache.TryGetValue(name, out var cached))
            return cached;

        foreach (var row in Catalog.Rows())
        {
            string? rowName = row.GetString("Name");
            if (rowName is null || !string.Equals(rowName, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!row.TryGetInt32("Type", out int type) || type != 1)
                continue;
            if (!row.TryGetInt32("Id", out int tdefPage))
                continue;

            var table = LoadTable(tdefPage, rowName);
            _tableCache[rowName] = table;
            return table;
        }

        throw new Jet4FormatException($"Table '{name}' was not found in '{Path.GetFileName(FilePath)}'.");
    }

    public Jet4Table? TryGetTable(string name) => HasTable(name) ? GetTable(name) : null;

    internal Jet4Table LoadTable(int tdefPageNumber, string name) => new(this, tdefPageNumber, name);
}
