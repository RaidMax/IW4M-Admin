using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Data.Extensions;

/// <summary>Preserves the existing SQLite binary format while ordering timestamps by UTC.</summary>
public sealed class UtcDateTimeOffsetToBinaryConverter() : ValueConverter<DateTimeOffset, long>(
    instant => DateTimeOffsetToBinaryConverter.ToLong(instant.ToUniversalTime()),
    binary => DateTimeOffsetToBinaryConverter.ToDateTimeOffset(binary).ToUniversalTime());
