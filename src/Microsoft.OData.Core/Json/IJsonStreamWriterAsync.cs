//---------------------------------------------------------------------
// <copyright file="IJsonStreamWriterAsync.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.OData.Json
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for writing JSON including streaming binary values.
    /// </summary>
    [CLSCompliant(false)]
    public interface IJsonStreamWriterAsync : IJsonWriterAsync
    {
        /// <summary>
        /// Start the stream property valuescope.
        /// </summary>
        /// <returns>
        /// A Stream to write to
        /// </returns>
        Task<Stream> StartStreamValueScopeAsync();

        /// <summary>
        /// Start the TextWriter value valuescope.
        /// </summary>
        /// <param name="contentType">ContentType of the string being written.</param>
        /// <returns>
        /// A Text writer to write to the stream.
        /// </returns>
        Task<TextWriter> StartTextWriterValueScopeAsync(string contentType);

        /// <summary>
        /// End the current stream property value scope.
        /// </summary>
        Task EndStreamValueScopeAsync();

        /// <summary>
        /// End the current TextWriter value valuescope.
        /// </summary>
        Task EndTextWriterValueScopeAsync();
    }
}
