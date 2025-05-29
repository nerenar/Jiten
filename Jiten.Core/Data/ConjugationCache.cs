using System.Collections.Concurrent;

namespace Jiten.Core.Data;

/// <summary>
/// Provides caching for conjugation strings to reduce memory usage across thousands of DeckWord instances.
/// Maps each unique conjugation string to a byte index and vice versa.
/// </summary>
public static class ConjugationCache
{
    private static readonly ConcurrentDictionary<string, byte> StringToByteMap = new();
    private static readonly List<string> ByteToStringMap = new();
    private static readonly Lock SyncLock = new();

    /// <summary>
    /// Gets the byte index for a conjugation string, adding it to the cache if it doesn't exist.
    /// </summary>
    /// <param name="conjugation">The conjugation string to cache</param>
    /// <returns>The byte index representing the conjugation</returns>
    public static byte GetOrAddByte(string conjugation)
    {
        // If the string is already in the cache, return its byte index
        if (StringToByteMap.TryGetValue(conjugation, out byte index))
        {
            return index;
        }

        // Add the string to the cache with a new byte index
        lock (SyncLock)
        {
            // Check again in case another thread added it while we were waiting
            if (StringToByteMap.TryGetValue(conjugation, out index))
            {
                return index;
            }

            // Make sure we don't exceed byte capacity (255 different conjugations)
            if (ByteToStringMap.Count >= 255)
            {
                throw new OverflowException("Conjugation cache has reached maximum capacity (255 unique conjugations)");
            }

            // Add the new string to the maps
            index = (byte)ByteToStringMap.Count;
            ByteToStringMap.Add(conjugation);
            StringToByteMap[conjugation] = index;
            return index;
        }
    }

    /// <summary>
    /// Gets the conjugation string for a given byte index.
    /// </summary>
    /// <param name="index">The byte index of the conjugation</param>
    /// <returns>The conjugation string</returns>
    public static string GetString(byte index)
    {
        if (index >= ByteToStringMap.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Conjugation index is out of range");
        }

        return ByteToStringMap[index];
    }

    /// <summary>
    /// Gets all cached conjugation strings.
    /// </summary>
    /// <returns>A read-only list of all cached conjugation strings</returns>
    public static IReadOnlyList<string> GetAllStrings() => ByteToStringMap.AsReadOnly();

    /// <summary>
    /// Gets the number of unique conjugation strings in the cache.
    /// </summary>
    public static int Count => ByteToStringMap.Count;
}