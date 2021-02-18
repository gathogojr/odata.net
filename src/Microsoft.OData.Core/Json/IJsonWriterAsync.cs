//---------------------------------------------------------------------
// <copyright file="IJsonWriterAsync.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.OData.Json
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.OData.Edm;

    /// <summary>
    /// Interface for a class that can write arbitrary JSON asynchronously.
    /// </summary>
    [CLSCompliant(false)]
    public interface IJsonWriterAsync
    {
        /// <summary>
        /// Start the padding function scope asynchronously.
        /// </summary>
        Task StartPaddingFunctionScopeAsync();

        /// <summary>
        /// End the padding function scope asynchronously.
        /// </summary>
        Task EndPaddingFunctionScopeAsync();

        /// <summary>
        /// Start the object scope asynchronously.
        /// </summary>
        Task StartObjectScopeAsync();

        /// <summary>
        /// End the current object scope asynchronously.
        /// </summary>
        Task EndObjectScopeAsync();

        /// <summary>
        /// Start the array scope asynchronously.
        /// </summary>
        Task StartArrayScopeAsync();

        /// <summary>
        /// End the current array scope asynchronously.
        /// </summary>
        Task EndArrayScopeAsync();

        /// <summary>
        /// Write the name for the object property asynchronously.
        /// </summary>
        /// <param name="name">Name of the object property.</param>
        Task WriteNameAsync(string name);

        /// <summary>
        /// Writes a function name for JSON padding asynchronously.
        /// </summary>
        /// <param name="functionName">Name of the padding function to write.</param>
        Task WritePaddingFunctionNameAsync(string functionName);

        /// <summary>
        /// Write a boolean value asynchronously.
        /// </summary>
        /// <param name="value">Boolean value to be written.</param>
        Task WriteValueAsync(bool value);

        /// <summary>
        /// Write an integer value asynchronously.
        /// </summary>
        /// <param name="value">Integer value to be written.</param>
        Task WriteValueAsync(int value);

        /// <summary>
        /// Write a float value asynchronously.
        /// </summary>
        /// <param name="value">Float value to be written.</param>
        Task WriteValueAsync(float value);

        /// <summary>
        /// Write a short value asynchronously.
        /// </summary>
        /// <param name="value">Short value to be written.</param>
        Task WriteValueAsync(short value);

        /// <summary>
        /// Write a long value asynchronously.
        /// </summary>
        /// <param name="value">Long value to be written.</param>
        Task WriteValueAsync(long value);

        /// <summary>
        /// Write a double value asynchronously.
        /// </summary>
        /// <param name="value">Double value to be written.</param>
        Task WriteValueAsync(double value);

        /// <summary>
        /// Write a Guid value asynchronously.
        /// </summary>
        /// <param name="value">Guid value to be written.</param>
        Task WriteValueAsync(Guid value);

        /// <summary>
        /// Write a decimal value asynchronously.
        /// </summary>
        /// <param name="value">Decimal value to be written.</param>
        Task WriteValueAsync(decimal value);

        /// <summary>
        /// Writes a DateTimeOffset value
        /// </summary>
        /// <param name="value">DateTimeOffset value to be written.</param>
        Task WriteValueAsync(DateTimeOffset value);

        /// <summary>
        /// Writes a TimeSpan value asynchronously.
        /// </summary>
        /// <param name="value">TimeSpan value to be written.</param>
        Task WriteValueAsync(TimeSpan value);

        /// <summary>
        /// Write a byte value asynchronously.
        /// </summary>
        /// <param name="value">Byte value to be written.</param>
        Task WriteValueAsync(byte value);

        /// <summary>
        /// Write an sbyte value asynchronously.
        /// </summary>
        /// <param name="value">SByte value to be written.</param>
        Task WriteValueAsync(sbyte value);

        /// <summary>
        /// Write a string value asynchronously.
        /// </summary>
        /// <param name="value">String value to be written.</param>
        Task WriteValueAsync(string value);

        /// <summary>
        /// Write a byte array asynchronously.
        /// </summary>
        /// <param name="value">Byte array to be written.</param>
        Task WriteValueAsync(byte[] value);

        /// <summary>
        /// Write a Date value asynchronously.
        /// </summary>
        /// <param name="value">Date value to be written.</param>
        Task WriteValueAsync(Date value);

        /// <summary>
        /// Write a TimeOfDay value asynchronously.
        /// </summary>
        /// <param name="value">TimeOfDay value to be written.</param>
        Task WriteValueAsync(TimeOfDay value);

        /// <summary>
        /// Write a raw value asynchronously.
        /// </summary>
        /// <param name="rawValue">Raw value to be written.</param>
        Task WriteRawValueAsync(string rawValue);

        /// <summary>
        /// Clears all buffers for the current writer asynchronously.
        /// </summary>
        Task FlushAsync();
    }
}
