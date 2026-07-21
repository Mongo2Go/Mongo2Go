using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MongoDownloader
{
    /// <summary>
    /// Streams the <c>versions</c> array of a MongoDB release feed and returns the first matching version, without
    /// holding the whole document in memory.
    /// </summary>
    /// <remarks>
    /// The full feed (<c>full.json</c>) is around 50 MB and lists more than a thousand versions, each with every
    /// platform download. We only ever need one version, so deserialising the entire document - as
    /// <see cref="System.Net.Http.Json.HttpClientJsonExtensions"/>.<c>GetFromJsonAsync</c> did - allocated hundreds of
    /// megabytes and crashed on memory-constrained machines. This reads the HTTP response as a stream and materialises
    /// one <see cref="Version"/> at a time, discarding those that do not match, so peak memory is a small buffer plus a
    /// single version regardless of feed size.
    /// </remarks>
    internal static class MongoReleaseReader
    {
        private static readonly JsonSerializerOptions SerializerOptions = new();

        // A version object with all its downloads is a few tens of kilobytes; the buffer grows if one is ever larger.
        private const int InitialBufferSize = 64 * 1024;

        public static Task<Version?> FindVersionAsync(Stream stream, Func<Version, bool> predicate, CancellationToken cancellationToken)
            => FindVersionAsync(stream, predicate, InitialBufferSize, cancellationToken);

        /// <summary>
        /// Overload with an explicit initial buffer size, used by tests to exercise the path where a single version
        /// spans the buffer and the reader has to rewind and refill.
        /// </summary>
        internal static async Task<Version?> FindVersionAsync(Stream stream, Func<Version, bool> predicate, int initialBufferSize, CancellationToken cancellationToken)
        {
            var buffer = new byte[initialBufferSize];
            var dataLength = 0;
            var readerState = new JsonReaderState();
            var navigation = Navigation.BeforeVersions;

            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(dataLength), cancellationToken).ConfigureAwait(false);
                var isFinalBlock = bytesRead == 0;
                dataLength += bytesRead;

                var stop = Scan(buffer.AsSpan(0, dataLength), isFinalBlock, predicate, ref readerState, ref navigation, out var consumed, out var match);
                if (stop)
                {
                    return match;
                }

                if (isFinalBlock)
                {
                    // The stream ended without a match.
                    return null;
                }

                // Move the not-yet-consumed bytes to the front and top the buffer up on the next read.
                var remaining = dataLength - consumed;
                if (consumed > 0)
                {
                    buffer.AsSpan(consumed, remaining).CopyTo(buffer);
                }
                dataLength = remaining;

                // A single version larger than the whole buffer cannot make progress until the buffer grows.
                if (dataLength == buffer.Length)
                {
                    Array.Resize(ref buffer, buffer.Length * 2);
                }
            }
        }

        private enum Navigation
        {
            BeforeVersions,
            ExpectingArrayStart,
            InsideArray,
        }

        /// <summary>
        /// Reads as many complete tokens as the buffer allows. Returns <c>true</c> when a decision has been reached
        /// (a match was found, or the array ended with no match); returns <c>false</c> to request more data, reporting
        /// how many bytes were fully consumed so the caller can compact the buffer.
        /// </summary>
        private static bool Scan(ReadOnlySpan<byte> data, bool isFinalBlock, Func<Version, bool> predicate, ref JsonReaderState readerState, ref Navigation navigation, out int consumed, out Version? match)
        {
            match = null;
            var reader = new Utf8JsonReader(data, isFinalBlock, readerState);

            // Navigate to the start of the "versions" array. This is entered at most once.
            while (navigation != Navigation.InsideArray)
            {
                if (!reader.Read())
                {
                    readerState = reader.CurrentState;
                    consumed = (int)reader.BytesConsumed;
                    return false;
                }

                if (navigation == Navigation.BeforeVersions)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("versions"u8))
                    {
                        navigation = Navigation.ExpectingArrayStart;
                    }
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    navigation = Navigation.InsideArray;
                }
            }

            // Iterate the array elements. safeConsumed/safeState always point at an element boundary, so if an element
            // is only partially buffered we can rewind to it and retry after topping the buffer up.
            var safeConsumed = (int)reader.BytesConsumed;
            var safeState = reader.CurrentState;

            while (true)
            {
                var lookahead = reader;
                if (!lookahead.Read())
                {
                    break;
                }

                if (lookahead.TokenType == JsonTokenType.EndArray)
                {
                    consumed = (int)lookahead.BytesConsumed;
                    return true; // array finished, match stays null
                }

                if (lookahead.TokenType != JsonTokenType.StartObject)
                {
                    // Not expected in this feed, but advance defensively rather than spin.
                    reader = lookahead;
                    safeConsumed = (int)reader.BytesConsumed;
                    safeState = reader.CurrentState;
                    continue;
                }

                // lookahead is positioned at the element's opening brace. Only deserialise once the whole object is
                // buffered, otherwise Deserialize would throw at the buffer boundary; safeConsumed still points before
                // this element, so an incomplete element is retried after the buffer is topped up.
                var completeness = lookahead;
                if (!completeness.TrySkip())
                {
                    break;
                }

                var element = lookahead;
                var version = JsonSerializer.Deserialize<Version>(ref element, SerializerOptions);

                reader = completeness; // positioned on the element's closing brace
                safeConsumed = (int)reader.BytesConsumed;
                safeState = reader.CurrentState;

                if (version != null && predicate(version))
                {
                    match = version;
                    consumed = safeConsumed;
                    return true;
                }
            }

            readerState = safeState;
            consumed = safeConsumed;
            return false;
        }
    }
}
