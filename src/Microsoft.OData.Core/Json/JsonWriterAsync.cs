//---------------------------------------------------------------------
// <copyright file="JsonWriterAsync.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.OData.Json
{
    #region Namespaces
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.OData.Buffers;
    using Microsoft.OData.Edm;
    #endregion Namespaces

    /// <summary>
    /// Writer for the JSON format. http://www.json.org
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "This class does not own the underlying stream/writer and thus should never dispose it.")]
    internal sealed partial class JsonWriter
    {
        /// <summary>
        /// Start the padding function scope.
        /// </summary>
        public async Task StartPaddingFunctionScopeAsync()
        {
            Debug.Assert(this.scopes.Count == 0, "Padding scope can only be the outer most scope.");
            await this.StartScopeAsync(ScopeType.Padding).ConfigureAwait(false);
        }

        /// <summary>
        /// End the padding function scope.
        /// </summary>
        public async Task EndPaddingFunctionScopeAsync()
        {
            Debug.Assert(this.scopes.Count > 0, "No scope to end.");

            await this.writer.WriteLineAsync().ConfigureAwait(false);
            await this.writer.DecreaseIndentationAsync().ConfigureAwait(false);
            Scope scope = this.scopes.Pop();

            Debug.Assert(scope.Type == ScopeType.Padding, "Ending scope does not match.");

            await this.writer.WriteAsync(scope.EndString).ConfigureAwait(false);
        }

        /// <summary>
        /// Start the object scope.
        /// </summary>
        public async Task StartObjectScopeAsync()
        {
            await this.StartScopeAsync(ScopeType.Object).ConfigureAwait(false);
        }

        /// <summary>
        /// End the current object scope.
        /// </summary>
        public async Task EndObjectScopeAsync()
        {
            Debug.Assert(this.scopes.Count > 0, "No scope to end.");

            await this.writer.WriteLineAsync().ConfigureAwait(false);
            await this.writer.DecreaseIndentationAsync().ConfigureAwait(false);
            Scope scope = this.scopes.Pop();

            Debug.Assert(scope.Type == ScopeType.Object, "Ending scope does not match.");

            await this.writer.WriteAsync(scope.EndString).ConfigureAwait(false);
        }

        /// <summary>
        /// Start the array scope.
        /// </summary>
        public async Task StartArrayScopeAsync()
        {
            await this.StartScopeAsync(ScopeType.Array).ConfigureAwait(false);
        }

        /// <summary>
        /// End the current array scope.
        /// </summary>
        public async Task EndArrayScopeAsync()
        {
            Debug.Assert(this.scopes.Count > 0, "No scope to end.");

            await this.writer.WriteLineAsync().ConfigureAwait(false);
            await this.writer.DecreaseIndentationAsync().ConfigureAwait(false);
            Scope scope = this.scopes.Pop();

            Debug.Assert(scope.Type == ScopeType.Array, "Ending scope does not match.");

            await this.writer.WriteAsync(scope.EndString).ConfigureAwait(false);
        }

        /// <summary>
        /// Write the name for the object property.
        /// </summary>
        /// <param name="name">Name of the object property.</param>
        public async Task WriteNameAsync(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "The name must be specified.");
            Debug.Assert(this.scopes.Count > 0, "There must be an active scope for name to be written.");
            Debug.Assert(this.scopes.Peek().Type == ScopeType.Object, "The active scope must be an object scope for name to be written.");

            Scope currentScope = this.scopes.Peek();
            if (currentScope.ObjectCount != 0)
            {
                await this.writer.WriteAsync(JsonConstants.ObjectMemberSeparator).ConfigureAwait(false);
            }

            currentScope.ObjectCount++;

            await this.writer.WriteEscapedJsonStringAsync(name, this.stringEscapeOption, this.wrappedBuffer)
                .ConfigureAwait(false);
            await this.writer.WriteAsync(JsonConstants.NameValueSeparator).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes a function name for JSON padding.
        /// </summary>
        /// <param name="functionName">Name of the padding function to write.</param>
        public async Task WritePaddingFunctionNameAsync(string functionName)
        {
            await this.writer.WriteAsync(functionName).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a boolean value.
        /// </summary>
        /// <param name="value">Boolean value to be written.</param>
        public async Task WriteValueAsync(bool value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write an integer value.
        /// </summary>
        /// <param name="value">Integer value to be written.</param>
        public async Task WriteValueAsync(int value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a float value.
        /// </summary>
        /// <param name="value">Float value to be written.</param>
        public async Task WriteValueAsync(float value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a short value.
        /// </summary>
        /// <param name="value">Short value to be written.</param>
        public async Task WriteValueAsync(short value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a long value.
        /// </summary>
        /// <param name="value">Long value to be written.</param>
        public async Task WriteValueAsync(long value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);

            // if it is IEEE754Compatible, write numbers with quotes; otherwise, write numbers directly.
            if (isIeee754Compatible)
            {
                await this.writer.WriteValueAsync(value.ToString(CultureInfo.InvariantCulture),
                    this.stringEscapeOption, this.wrappedBuffer).ConfigureAwait(false);
            }
            else
            {
                await this.writer.WriteValueAsync(value).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Write a double value.
        /// </summary>
        /// <param name="value">Double value to be written.</param>
        public async Task WriteValueAsync(double value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a Guid value.
        /// </summary>
        /// <param name="value">Guid value to be written.</param>
        public async Task WriteValueAsync(Guid value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a decimal value
        /// </summary>
        /// <param name="value">Decimal value to be written.</param>
        public async Task WriteValueAsync(decimal value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);

            // if it is not IEEE754Compatible, write numbers directly without quotes;
            if (isIeee754Compatible)
            {
                await this.writer.WriteValueAsync(value.ToString(CultureInfo.InvariantCulture),
                    this.stringEscapeOption, this.wrappedBuffer).ConfigureAwait(false);
            }
            else
            {
                await this.writer.WriteValueAsync(value).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Writes a DateTimeOffset value
        /// </summary>
        /// <param name="value">DateTimeOffset value to be written.</param>
        public async Task WriteValueAsync(DateTimeOffset value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value, ODataJsonDateTimeFormat.ISO8601DateTime)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Writes a TimeSpan value
        /// </summary>
        /// <param name="value">TimeSpan value to be written.</param>
        public async Task WriteValueAsync(TimeSpan value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a Date value
        /// </summary>
        /// <param name="value">Date value to be written.</param>
        public async Task WriteValueAsync(TimeOfDay value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a Date value
        /// </summary>
        /// <param name="value">Date value to be written.</param>
        public async Task WriteValueAsync(Date value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a byte value.
        /// </summary>
        /// <param name="value">Byte value to be written.</param>
        public async Task WriteValueAsync(byte value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write an sbyte value.
        /// </summary>
        /// <param name="value">SByte value to be written.</param>
        public async Task WriteValueAsync(sbyte value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a string value.
        /// </summary>
        /// <param name="value">String value to be written.</param>
        public async Task WriteValueAsync(string value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value, this.stringEscapeOption, this.wrappedBuffer)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Write a byte array.
        /// </summary>
        /// <param name="value">Byte array to be written.</param>
        public async Task WriteValueAsync(byte[] value)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteValueAsync(value, this.wrappedBuffer, ArrayPool).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a raw value.
        /// </summary>
        /// <param name="rawValue">Raw value to be written.</param>
        public async Task WriteRawValueAsync(string rawValue)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteAsync(rawValue).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears all buffers for the current writer.
        /// </summary>
        public async Task FlushAsync()
        {
            await this.writer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Start the stream property valuescope.
        /// </summary>
        /// <returns>The stream to write the property value to</returns>
        public async Task<Stream> StartStreamValueScopeAsync()
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            await this.writer.WriteAsync(JsonConstants.QuoteCharacter).ConfigureAwait(false);
            await this.writer.FlushAsync().ConfigureAwait(false);
            // IMPORTANT: The Dispose method of the returned stream does the following:
            // - Writes trailing bytes to the writer synchronously
            // - Flushes the buffer of the writer synchronously
            // ODL supports net45 and netstandard1.1 (in addition to .netstandard2.0)
            // This makes it complicated to implement IAsyncDisposable in ODataBinaryStreamWriter
            // TODO: Can the returned stream be safely used asynchronously?
            this.binaryValueStream = new ODataBinaryStreamWriter(writer, this.wrappedBuffer, ArrayPool);
            return this.binaryValueStream;
        }

        /// <summary>
        /// End the current stream property value scope.
        /// </summary>
        public async Task EndStreamValueScopeAsync()
        {
            await this.binaryValueStream.FlushAsync().ConfigureAwait(false);
            this.binaryValueStream.Dispose();
            this.binaryValueStream = null;
            await this.writer.FlushAsync().ConfigureAwait(false);
            await this.writer.WriteAsync(JsonConstants.QuoteCharacter).ConfigureAwait(false);
        }

        /// <summary>
        /// Start the TextWriter valuescope.
        /// </summary>
        /// <param name="contentType">ContentType of the string being written.</param>
        /// <returns>The textwriter to write the text property value to</returns>
        public async Task<TextWriter> StartTextWriterValueScopeAsync(string contentType)
        {
            await this.WriteValueSeparatorAsync().ConfigureAwait(false);
            this.currentContentType = contentType;
            if (!IsWritingJson)
            {
                await this.writer.WriteAsync(JsonConstants.QuoteCharacter)
                    .ConfigureAwait(false);
                await this.writer.FlushAsync().ConfigureAwait(false);
                return new ODataJsonTextWriter(writer, this.wrappedBuffer, this.ArrayPool);
            }

            await this.writer.FlushAsync().ConfigureAwait(false);

            return this.writer;
        }

        /// <summary>
        /// End the TextWriter valuescope.
        /// </summary>
        public async Task EndTextWriterValueScopeAsync()
        {
            if (!IsWritingJson)
            {
                await this.writer.WriteAsync(JsonConstants.QuoteCharacter)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Writes a separator of a value asynchronously if it's needed for the next value to be written.
        /// </summary>
        private async Task WriteValueSeparatorAsync()
        {
            if (this.scopes.Count == 0)
            {
                return;
            }

            Scope currentScope = this.scopes.Peek();
            if (currentScope.Type == ScopeType.Array)
            {
                if (currentScope.ObjectCount != 0)
                {
                    await this.writer.WriteAsync(JsonConstants.ArrayElementSeparator)
                        .ConfigureAwait(false);
                }

                currentScope.ObjectCount++;
            }
        }

        /// <summary>
        /// Start the scope asynchronously given the scope type.
        /// </summary>
        /// <param name="type">The scope type to start.</param>
        private async Task StartScopeAsync(ScopeType type)
        {
            if (this.scopes.Count != 0 && this.scopes.Peek().Type != ScopeType.Padding)
            {
                Scope currentScope = this.scopes.Peek();
                if ((currentScope.Type == ScopeType.Array) &&
                    (currentScope.ObjectCount != 0))
                {
                    await this.writer.WriteAsync(JsonConstants.ArrayElementSeparator)
                        .ConfigureAwait(false);
                }

                currentScope.ObjectCount++;
            }

            Scope scope = new Scope(type);
            this.scopes.Push(scope);

            await this.writer.WriteAsync(scope.StartString).ConfigureAwait(false);
            await this.writer.IncreaseIndentationAsync().ConfigureAwait(false);
            await this.writer.WriteLineAsync().ConfigureAwait(false);
        }
    }
}
