using System.Buffers.Binary;
using System.Text;

namespace PunchPoint.Core.Jet;

/// <summary>A table definition plus row enumeration for a Jet 4 database.</summary>
public sealed class Jet4Table
{
    // ---- Jet 4 layout constants (see NOTICE.md / Jackcess Jet4Format) ----
    private const byte PageTypeData = 0x01;
    private const byte PageTypeTableDef = 0x02;
    private const byte PageTypeUsageMap = 0x05;

    private const int OffsetNextTdefPage = 0x04;
    private const int OffsetNumRows = 0x10;
    private const int OffsetMaxCols = 0x29;
    private const int OffsetNumVarCols = 0x2B;
    private const int OffsetNumCols = 0x2D;
    private const int OffsetNumLogicalIndexes = 0x2F;
    private const int OffsetNumIndexes = 0x33;
    private const int OffsetOwnedPages = 0x37;
    private const int OffsetFreeSpacePages = 0x3B;
    private const int OffsetIndexDefBlock = 0x3F;

    private const int SizeIndexDefinition = 12;
    private const int SizeColumnHeader = 25;

    private const int OffsetRowStart = 0x0E;
    private const int SizeRowLocation = 2;
    private const int OffsetNumRowsOnDataPage = 0x0C;

    private const short RowOffsetMask = 0x1FFF;
    private const short DeletedRowMask = unchecked((short)0x8000);
    private const short OverflowRowMask = 0x4000;

    private readonly Jet4Database _db;
    private readonly byte[] _definition;
    private readonly int[] _ownedPages;
    private int _rowsOnDefPage;

    internal Jet4Table(Jet4Database db, int tdefPageNumber, string name)
    {
        _db = db;
        Name = name;
        TdefPageNumber = tdefPageNumber;

        _definition = LoadDefinition(tdefPageNumber);

        int columnCount = BinaryPrimitives.ReadInt16LittleEndian(_definition.AsSpan(OffsetNumCols, 2));
        int indexCount = BinaryPrimitives.ReadInt32LittleEndian(_definition.AsSpan(OffsetNumIndexes, 4));
        _rowsOnDefPage = BinaryPrimitives.ReadInt32LittleEndian(_definition.AsSpan(OffsetNumRows, 4));

        var columns = new List<Jet4Column>(columnCount);
        int colBlock = OffsetIndexDefBlock + indexCount * SizeIndexDefinition;

        // Column names follow all column definition blocks, each prefixed with a byte length.
        int nameOffset = colBlock + columnCount * SizeColumnHeader;
        var names = new List<string>(columnCount);
        for (int i = 0; i < columnCount; i++)
        {
            int byteLen = BinaryPrimitives.ReadUInt16LittleEndian(_definition.AsSpan(nameOffset, 2));
            nameOffset += 2;
            names.Add(Encoding.Unicode.GetString(_definition, nameOffset, byteLen));
            nameOffset += byteLen;
        }

        for (int i = 0; i < columnCount; i++)
        {
            int off = colBlock + i * SizeColumnHeader;
            var type = (JetColumnType)_definition[off];
            byte flags = _definition[off + 15];
            columns.Add(new Jet4Column
            {
                Name = names[i],
                Type = type,
                Number = BinaryPrimitives.ReadInt16LittleEndian(_definition.AsSpan(off + 5, 2)),
                VarIndex = BinaryPrimitives.ReadInt16LittleEndian(_definition.AsSpan(off + 7, 2)),
                FixedOffset = BinaryPrimitives.ReadInt16LittleEndian(_definition.AsSpan(off + 21, 2)),
                Length = BinaryPrimitives.ReadInt16LittleEndian(_definition.AsSpan(off + 23, 2)),
                Precision = _definition[off + 11],
                Scale = _definition[off + 12],
                IsVariable = (flags & 0x01) == 0,
            });
        }

        Columns = columns;
        _ownedPages = ReadOwnedPages();
    }

    public string Name { get; }
    public int TdefPageNumber { get; }
    public IReadOnlyList<Jet4Column> Columns { get; }

    /// <summary>Row count recorded in the table definition (may be stale; prefer counting rows).</summary>
    public int RowsOnDefinitionPage => _rowsOnDefPage;

    public IEnumerable<Jet4Row> Rows()
    {
        foreach (int pageNumber in _ownedPages)
        {
            // Only data pages hold rows. (Usage-map pages are not listed here, but be defensive.)
            if (_db.PageType(pageNumber) != PageTypeData)
                continue;

            int rowsOnPage = RowsOnDataPage(pageNumber);
            var decoded = new List<Jet4Row>(Math.Max(rowsOnPage, 0));

            for (int rowNumber = 0; rowNumber < rowsOnPage; rowNumber++)
            {
                Jet4Row? row;
                try
                {
                    row = ReadRow(pageNumber, rowNumber);
                }
                catch (Exception ex)
                {
                    throw new Jet4FormatException(
                        $"Failed reading row {rowNumber} on page {pageNumber} of table '{Name}': {ex.Message}", ex);
                }
                if (row is not null)
                    decoded.Add(row);
            }

            foreach (var row in decoded)
                yield return row;
        }
    }

    private int RowsOnDataPage(int pageNumber)
    {
        var page = _db.Page(pageNumber);
        return BinaryPrimitives.ReadInt16LittleEndian(page.Slice(OffsetNumRowsOnDataPage, 2));
    }

    public int CountRows()
    {
        int count = 0;
        foreach (var _ in Rows()) count++;
        return count;
    }

    // -------------------------------------------------------------------

    private byte[] LoadDefinition(int firstPage)
    {
        // A table definition may span several pages; pages after the first contribute their
        // bytes from offset 8 onwards.
        var pages = new List<int> { firstPage };
        int next = BinaryPrimitives.ReadInt32LittleEndian(_db.Page(firstPage).Slice(OffsetNextTdefPage, 4));

        int guard = 0;
        while (next != 0)
        {
            if (++guard > 10_000)
                throw new Jet4FormatException($"Table definition page chain for '{Name}' is too long (corrupt file).");
            pages.Add(next);
            next = BinaryPrimitives.ReadInt32LittleEndian(_db.Page(next).Slice(OffsetNextTdefPage, 4));
        }

        if (_db.Page(firstPage)[0] != PageTypeTableDef)
            throw new Jet4FormatException($"Page {firstPage} is not a table definition page.");

        var buffer = new byte[pages.Count * Jet4Database.PageSize];
        _db.Page(pages[0]).CopyTo(buffer.AsSpan(0, Jet4Database.PageSize));
        for (int i = 1; i < pages.Count; i++)
            _db.Page(pages[i])[8..].CopyTo(buffer.AsSpan(i * Jet4Database.PageSize, Jet4Database.PageSize - 8));

        // Trim to the declared definition length when it is sane.
        if (buffer.Length >= 12)
        {
            int declared = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(8, 4));
            if (declared > 0 && declared <= buffer.Length)
                Array.Resize(ref buffer, declared);
        }

        return buffer;
    }

    private int[] ReadOwnedPages()
    {
        // Usage map declaration lives in the table definition at a page/row reference.
        int rowNumber = _definition[OffsetOwnedPages];
        int pageNumber = Read3ByteInt(_definition, OffsetOwnedPages + 1);
        return ReadUsageMap(pageNumber, rowNumber).ToArray();
    }

    private static int Read3ByteInt(ReadOnlySpan<byte> span, int offset) =>
        span[offset] | (span[offset + 1] << 8) | (span[offset + 2] << 16);

    private List<int> ReadUsageMap(int pageNumber, int rowNumber)
    {
        var result = new List<int>();
        if (pageNumber <= 0) return result;

        var page = _db.Page(pageNumber);
        (int start, int end) = FindRowBounds(page, rowNumber);
        byte mapType = page[start];

        if (mapType == 0x00)
        {
            // Inline bitmap: 32-bit first page, then one bit per page.
            int firstPage = BinaryPrimitives.ReadInt32LittleEndian(page.Slice(start + 1, 4));
            for (int i = start + 5; i < end; i++)
            {
                byte b = page[i];
                if (b == 0) continue;
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((b & (1 << bit)) != 0)
                        result.Add(firstPage + (i - (start + 5)) * 8 + bit);
                }
            }
        }
        else if (mapType == 0x01)
        {
            // Reference map: one or more usage-map pages, each holding a bitmap.
            const int maxPagesPerMapPage = (Jet4Database.PageSize - 4) * 8;
            int numMapPages = (end - start - 1) / 4;
            for (int i = 0; i < numMapPages; i++)
            {
                int mapPageNumber = BinaryPrimitives.ReadInt32LittleEndian(page.Slice(start + 1 + i * 4, 4));
                if (mapPageNumber <= 0) continue;

                var mapPage = _db.Page(mapPageNumber);
                if (mapPage[0] != PageTypeUsageMap)
                    throw new Jet4FormatException($"Expected a usage map page at {mapPageNumber}.");

                for (int j = 4; j < Jet4Database.PageSize; j++)
                {
                    byte b = mapPage[j];
                    if (b == 0) continue;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        if ((b & (1 << bit)) != 0)
                            result.Add(i * maxPagesPerMapPage + (j - 4) * 8 + bit);
                    }
                }
            }
        }
        else
        {
            throw new Jet4FormatException($"Unrecognized usage map type {mapType} on page {pageNumber}.");
        }

        return result;
    }

    private static (int Start, int End) FindRowBounds(ReadOnlySpan<byte> page, int rowNumber)
    {
        int startOffset = OffsetRowStart + SizeRowLocation * rowNumber;
        if (startOffset + 2 > page.Length)
            throw new Jet4FormatException("Row offset is outside the page.");

        short raw = BinaryPrimitives.ReadInt16LittleEndian(page.Slice(startOffset, 2));
        int start = raw & RowOffsetMask;

        int end;
        if (rowNumber == 0)
        {
            end = Jet4Database.PageSize;
        }
        else
        {
            short prev = BinaryPrimitives.ReadInt16LittleEndian(page.Slice(OffsetRowStart + SizeRowLocation * (rowNumber - 1), 2));
            end = prev & RowOffsetMask;
        }

        return (start, end);
    }

    private Jet4Row? ReadRow(int pageNumber, int rowNumber)
    {
        bool first = true;
        ReadOnlySpan<byte> page;

        while (true)
        {
            page = _db.Page(pageNumber);
            if (rowNumber < 0 || OffsetRowStart + SizeRowLocation * rowNumber + 2 > page.Length)
                return null;

            short raw = BinaryPrimitives.ReadInt16LittleEndian(page.Slice(OffsetRowStart + SizeRowLocation * rowNumber, 2));

            if (first && (raw & DeletedRowMask) != 0)
                return null; // deleted row
            first = false;

            int start = raw & RowOffsetMask;
            int end;
            if (rowNumber == 0)
            {
                end = Jet4Database.PageSize;
            }
            else
            {
                short prev = BinaryPrimitives.ReadInt16LittleEndian(page.Slice(OffsetRowStart + SizeRowLocation * (rowNumber - 1), 2));
                end = prev & RowOffsetMask;
            }

            if (end <= start || end > Jet4Database.PageSize)
                return null;

            if ((raw & OverflowRowMask) != 0)
            {
                // Row continues on another page.
                if (end - start < 4)
                    throw new Jet4FormatException("Invalid overflow row pointer.");
                int overflowRow = page[start];
                int overflowPage = Read3ByteInt(page, start + 1);
                pageNumber = overflowPage;
                rowNumber = overflowRow;
                continue;
            }

            return DecodeRow(page[start..end], rowNumber, pageNumber);
        }
    }

    private Jet4Row DecodeRow(ReadOnlySpan<byte> row, int rowNumber, int pageNumber)
    {
        var values = new Dictionary<string, object?>(Columns.Count, StringComparer.OrdinalIgnoreCase);

        if (row.Length < 4)
            return new Jet4Row(values, rowNumber, pageNumber);

        int columnCount = BinaryPrimitives.ReadUInt16LittleEndian(row);
        int maskSize = (columnCount + 7) / 8;
        int maskOffset = row.Length - maskSize;
        if (maskOffset < 2)
            return new Jet4Row(values, rowNumber, pageNumber);

        ReadOnlySpan<byte> nullMask = row[maskOffset..];
        int varBase = maskOffset; // offsets are stored just before the null mask

        foreach (var column in Columns)
        {
            if (column.Number >= columnCount)
            {
                values[column.Name] = null;
                continue;
            }

            bool bitSet = (nullMask[column.Number / 8] & (1 << (column.Number % 8))) != 0;

            if (column.Type == JetColumnType.Boolean)
            {
                values[column.Name] = bitSet;
                continue;
            }

            if (!bitSet)
            {
                values[column.Name] = null;
                continue;
            }

            ReadOnlySpan<byte> data;
            if (!column.IsVariable)
            {
                int pos = 2 + column.FixedOffset;
                int len = column.FixedSize;
                if (pos + len > row.Length)
                {
                    values[column.Name] = null;
                    continue;
                }
                data = row.Slice(pos, len);
            }
            else
            {
                int startPos = varBase - 4 - column.VarIndex * 2;
                int endPos = startPos - 2;
                if (startPos < 0 || endPos < 0)
                {
                    values[column.Name] = null;
                    continue;
                }
                int vStart = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(startPos, 2));
                int vEnd = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(endPos, 2));
                if (vStart > vEnd || vEnd > row.Length || vStart < 0)
                {
                    values[column.Name] = null;
                    continue;
                }
                data = row[vStart..vEnd];
            }

            values[column.Name] = DecodeValue(column, data, row);
        }

        return new Jet4Row(values, rowNumber, pageNumber);
    }

    private object? DecodeValue(Jet4Column column, ReadOnlySpan<byte> data, ReadOnlySpan<byte> row)
    {
        switch (column.Type)
        {
            case JetColumnType.Boolean:
                return false; // handled through the null mask

            case JetColumnType.Byte:
                return data.Length >= 1 ? data[0] : null;
            case JetColumnType.Int16:
                return data.Length >= 2 ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
            case JetColumnType.Int32:
                return data.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(data) : null;
            case JetColumnType.BigInt:
                return data.Length >= 8 ? BinaryPrimitives.ReadInt64LittleEndian(data) : null;
            case JetColumnType.Single:
                return data.Length >= 4 ? BinaryPrimitives.ReadSingleLittleEndian(data) : null;
            case JetColumnType.Double:
                return data.Length >= 8 ? BinaryPrimitives.ReadDoubleLittleEndian(data) : null;
            case JetColumnType.Currency:
                return data.Length >= 8
                    ? BinaryPrimitives.ReadInt64LittleEndian(data) / 10000m
                    : null;
            case JetColumnType.DateTime:
                return data.Length >= 8
                    ? JetDates.FromOleAutomation(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(data)))
                    : null;

            case JetColumnType.Text:
                return DecodeText(data);

            case JetColumnType.Memo:
                return DecodeLongText(data);
            case JetColumnType.Ole:
                return data.ToArray();

            case JetColumnType.Numeric:
                return DecodeNumeric(data, column.Scale);

            case JetColumnType.Guid:
                return data.Length >= 16 ? new Guid(data[..16]) : null;

            case JetColumnType.Binary:
            case JetColumnType.BigBinary:
                return data.ToArray();

            default:
                return null;
        }
    }

    private static decimal? DecodeNumeric(ReadOnlySpan<byte> data, int scale)
    {
        if (data.Length < 17) return null;

        byte signByte = data[0];
        var magnitude = data.Slice(1, 16).ToArray();

        // The 16-byte magnitude is stored as four little-endian 32-bit words.
        for (int i = 0; i < magnitude.Length; i += 4)
            Array.Reverse(magnitude, i, 4);

        var value = new System.Numerics.BigInteger(magnitude, isUnsigned: true, isBigEndian: true);
        decimal result = (decimal)value;
        if (scale > 0)
        {
            for (int i = 0; i < scale && i < 28; i++) result /= 10m;
        }
        return signByte != 0 ? -result : result;
    }

    /// <summary>
    /// Decodes a "long value" (Memo) column. The value bytes start with a 12-byte
    /// definition; the text itself may be inline right after it, on a single other
    /// page, or chained across several pages.
    /// </summary>
    private string DecodeLongText(ReadOnlySpan<byte> valueBytes)
    {
        if (valueBytes.Length < 4) return string.Empty;

        int lengthWithFlags = BinaryPrimitives.ReadInt32LittleEndian(valueBytes);
        int length = lengthWithFlags & 0x3FFFFFFF;
        byte type = (byte)((lengthWithFlags & 0xC0000000) >> 24);

        if (type == 0x80)
        {
            int available = valueBytes.Length - 12;
            if (available <= 0) return string.Empty;
            return DecodeText(valueBytes.Slice(12, Math.Min(length, available)));
        }

        if (valueBytes.Length < 12) return string.Empty;

        int rowNumber = valueBytes[4];
        int pageNumber = Read3ByteInt(valueBytes, 5);
        if (pageNumber <= 0) return string.Empty;

        var buffer = new byte[length];
        int written = 0;

        if (type == 0x40)
        {
            var page = _db.Page(pageNumber);
            (int start, int end) = FindRowBounds(page, rowNumber);
            int n = Math.Min(length, end - start);
            page.Slice(start, n).CopyTo(buffer);
            written = n;
        }
        else if (type == 0x00)
        {
            while (written < length && pageNumber > 0)
            {
                var page = _db.Page(pageNumber);
                (int start, int end) = FindRowBounds(page, rowNumber);

                rowNumber = page[start];
                pageNumber = Read3ByteInt(page, start + 1);

                int chunk = end - start - 4;
                if (chunk <= 0) break;
                int n = Math.Min(chunk, length - written);
                page.Slice(start + 4, n).CopyTo(buffer.AsSpan(written));
                written += n;
            }
        }

        if (written == 0) return string.Empty;
        return DecodeText(written == length ? buffer : buffer.AsSpan(0, written));
    }

    internal static string DecodeText(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return string.Empty;

        // 0xFF 0xFE marks a run-length style mix of compressed (1 byte/char) and
        // uncompressed (UTF-16LE) segments, toggled by a 0x00 byte.
        bool compressed = data.Length > 1 && data[0] == 0xFF && data[1] == 0xFE;
        if (!compressed)
            return Encoding.Unicode.GetString(data).TrimEnd('\0');

        var sb = new StringBuilder(data.Length);
        int start = 2;
        int pos = 2;
        bool inCompressedMode = true;

        while (pos < data.Length)
        {
            if (data[pos] == 0x00)
            {
                AppendSegment(data[start..pos], inCompressedMode, sb);
                inCompressedMode = !inCompressedMode;
                pos++;
                start = pos;
            }
            else
            {
                pos++;
            }
        }
        AppendSegment(data[start..pos], inCompressedMode, sb);
        return sb.ToString();
    }

    private static void AppendSegment(ReadOnlySpan<byte> segment, bool compressed, StringBuilder sb)
    {
        if (segment.Length == 0) return;
        if (compressed)
        {
            foreach (byte b in segment) sb.Append((char)b);
        }
        else
        {
            sb.Append(Encoding.Unicode.GetString(segment));
        }
    }
}
