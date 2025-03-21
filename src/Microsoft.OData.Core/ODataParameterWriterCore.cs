//---------------------------------------------------------------------
// <copyright file="ODataParameterWriterCore.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.OData
{
    #region Namespaces
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.OData.Core;
    using Microsoft.OData.Edm;
    using Microsoft.OData.Metadata;
    #endregion Namespaces

    /// <summary>
    /// Base class for OData parameter writers that verifies a proper sequence of write calls on the writer.
    /// </summary>
    internal abstract class ODataParameterWriterCore : ODataParameterWriter, IODataReaderWriterListener, IODataOutputInStreamErrorListener
    {
        /// <summary>The output context to write to.</summary>
        private readonly ODataOutputContext outputContext;

        /// <summary>The operation whose parameters will be written.</summary>
        private readonly IEdmOperation operation;

        /// <summary>Stack of writer scopes to keep track of the current context of the writer.</summary>
        private Stack<ParameterWriterState> scopes = new Stack<ParameterWriterState>();

        /// <summary>Parameter names that have already been written, used to detect duplicate writes on a parameter.</summary>
        private HashSet<string> parameterNamesWritten = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Checker to detect duplicate property names on complex parameter values.</summary>
        private IDuplicatePropertyNameChecker duplicatePropertyNameChecker;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="outputContext">The output context to write to.</param>
        /// <param name="operation">The operation import whose parameters will be written.</param>
        protected ODataParameterWriterCore(ODataOutputContext outputContext, IEdmOperation operation)
        {
            Debug.Assert(outputContext != null, "outputContext != null");

            this.outputContext = outputContext;
            this.operation = operation;
            this.scopes.Push(ParameterWriterState.Start);
        }

        /// <summary>
        /// An enumeration representing the current state of the writer.
        /// </summary>
        private enum ParameterWriterState
        {
            /// <summary>The writer is at the start; nothing has been written yet.</summary>
            Start,

            /// <summary>
            /// The writer is in a state where the next parameter can be written.
            /// The writer enters this state after WriteStart() or after the previous parameter is written.
            /// </summary>
            CanWriteParameter,

            /// <summary>One of the create writer method has been called and the created sub writer is not in Completed state.</summary>
            ActiveSubWriter,

            /// <summary>The writer has completed; nothing can be written anymore.</summary>
            Completed,

            /// <summary>An error had occurred while writing the payload; nothing can be written anymore.</summary>
            Error
        }

        /// <summary>Checker to detect duplicate property names on complex parameter values.</summary>
        protected IDuplicatePropertyNameChecker DuplicatePropertyNameChecker
        {
            get
            {
                return this.duplicatePropertyNameChecker ??
                       (this.duplicatePropertyNameChecker =
                           outputContext.MessageWriterSettings.Validator
                           .GetDuplicatePropertyNameChecker());
            }
        }

        /// <summary>
        /// The current state of the writer.
        /// </summary>
        private ParameterWriterState State
        {
            get { return this.scopes.Peek(); }
        }

        /// <summary>
        /// Flushes the write buffer to the underlying stream.
        /// </summary>
        public sealed override void Flush()
        {
            this.VerifyCanFlush(true /*synchronousCall*/);

            // make sure we switch to writer state Error if an exception is thrown during flushing.
            this.InterceptException((thisParam) => thisParam.FlushSynchronously());
        }


        /// <summary>
        /// Asynchronously flushes the write buffer to the underlying stream.
        /// </summary>
        /// <returns>A task instance that represents the asynchronous operation.</returns>
        public sealed override Task FlushAsync()
        {
            this.VerifyCanFlush(false /*synchronousCall*/);

            // make sure we switch to writer state Error if an exception is thrown during flushing.
            return this.InterceptExceptionAsync((thisParam) => thisParam.FlushAsynchronously());
        }

        /// <summary>
        /// Start writing a parameter payload.
        /// </summary>
        public sealed override void WriteStart()
        {
            this.VerifyCanWriteStart(true /*synchronousCall*/);
            this.InterceptException((thisParam) => thisParam.WriteStartImplementation());
        }


        /// <summary>
        /// Asynchronously start writing a parameter payload.
        /// </summary>
        /// <returns>A task instance that represents the asynchronous write operation.</returns>
        public sealed override Task WriteStartAsync()
        {
            this.VerifyCanWriteStart(false /*synchronousCall*/);
            return this.InterceptExceptionAsync(
                (thisParam) => thisParam.WriteStartImplementationAsync());
        }

        /// <summary>
        /// Start writing a value parameter.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="parameterValue">The value of the parameter to write (null/ODataEnumValue/primitiveClrValue).</param>
        public sealed override void WriteValue(string parameterName, object parameterValue)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference expectedTypeReference = this.VerifyCanWriteValueParameter(true /*synchronousCall*/, parameterName, parameterValue);
            this.InterceptException(
                (thisParam, parameterNameParam, parameterValueParam, expectedTypeReferenceParam) => 
                    thisParam.WriteValueImplementation(parameterName, parameterValue, expectedTypeReference),
                parameterName, parameterValue, expectedTypeReference);
        }


        /// <summary>
        /// Asynchronously start writing a value parameter.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="parameterValue">The value of the parameter to write.</param>
        /// <returns>A task instance that represents the asynchronous write operation.</returns>
        public sealed override Task WriteValueAsync(string parameterName, object parameterValue)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference expectedTypeReference = this.VerifyCanWriteValueParameter(false /*synchronousCall*/, parameterName, parameterValue);
            return this.InterceptExceptionAsync(
                (thisParam, parameterNameParam, parameterValueParam, expectedTypeReferenceParam) => 
                    thisParam.WriteValueImplementationAsync(parameterNameParam, parameterValueParam, expectedTypeReferenceParam),
                parameterName, parameterValue, expectedTypeReference);
        }

        /// <summary>
        /// Creates an <see cref="ODataCollectionWriter"/> to write the value of a collection parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <returns>The newly created <see cref="ODataCollectionWriter"/>.</returns>
        public sealed override ODataCollectionWriter CreateCollectionWriter(string parameterName)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference itemTypeReference = this.VerifyCanCreateCollectionWriter(true /*synchronousCall*/, parameterName);
            return this.InterceptException(
                (thisParam, parameterNameParam, itemTypeReferenceParam) => 
                    thisParam.CreateCollectionWriterImplementation(parameterNameParam, itemTypeReferenceParam),
                parameterName, itemTypeReference);
        }


        /// <summary>
        /// Asynchronously creates an <see cref="ODataCollectionWriter"/> to write the value of a collection parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <returns>A running task for the created writer.</returns>
        public sealed override Task<ODataCollectionWriter> CreateCollectionWriterAsync(string parameterName)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference itemTypeReference = this.VerifyCanCreateCollectionWriter(false /*synchronousCall*/, parameterName);

            return this.InterceptExceptionAsync(
                (thisParam, parameterNameParam, itemTypeReferenceParam) =>
                    thisParam.CreateCollectionWriterImplementationAsync(parameterNameParam, itemTypeReferenceParam),
                parameterName, itemTypeReference);
        }

        /// <summary> Creates an <see cref="Microsoft.OData.ODataWriter" /> to write a resource. </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <returns>The created writer.</returns>
        public sealed override ODataWriter CreateResourceWriter(string parameterName)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference itemTypeReference = this.VerifyCanCreateResourceWriter(true /*synchronousCall*/, parameterName);
            return this.InterceptException(
                (thisParam, parameterNameParam, itemTypeReferenceParam) =>
                    thisParam.CreateResourceWriterImplementation(parameterNameParam, itemTypeReferenceParam),
                parameterName, itemTypeReference);
        }


        /// <summary>Asynchronously creates an <see cref="ODataWriter" /> to  write a resource.</summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <returns>The asynchronously created <see cref="ODataWriter" />.</returns>
        public sealed override Task<ODataWriter> CreateResourceWriterAsync(string parameterName)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference itemTypeReference = this.VerifyCanCreateResourceWriter(false /*synchronousCall*/, parameterName);

            return this.InterceptExceptionAsync(
                (thisParam, parameterNameParam, itemTypeReferenceParam) =>
                    thisParam.CreateResourceWriterImplementationAsync(parameterNameParam, itemTypeReferenceParam),
                parameterName, itemTypeReference);
        }

        /// <summary> Creates an <see cref="Microsoft.OData.ODataWriter" /> to write a resource set. </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <returns>The created writer.</returns>
        public sealed override ODataWriter CreateResourceSetWriter(string parameterName)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference itemTypeReference = this.VerifyCanCreateResourceSetWriter(true /*synchronousCall*/, parameterName);
            return this.InterceptException(
                (thisParam, parameterNameParam, itemTypeReferenceParam) =>
                    thisParam.CreateResourceSetWriterImplementation(parameterNameParam, itemTypeReferenceParam),
                parameterName, itemTypeReference);
        }


        /// <summary>Asynchronously creates an <see cref="ODataWriter" /> to  write a resource set.</summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <returns>The asynchronously created <see cref="ODataWriter" />.</returns>
        public sealed override Task<ODataWriter> CreateResourceSetWriterAsync(string parameterName)
        {
            ExceptionUtils.CheckArgumentStringNotNullOrEmpty(parameterName, "parameterName");
            IEdmTypeReference itemTypeReference = this.VerifyCanCreateResourceSetWriter(false /*synchronousCall*/, parameterName);

            return this.InterceptExceptionAsync(
                (thisParam, parameterNameParam, itemTypeReferenceParam) =>
                    thisParam.CreateResourceSetWriterImplementationAsync(parameterNameParam, itemTypeReferenceParam),
                parameterName, itemTypeReference);
        }

        /// <summary>
        /// Finish writing a parameter payload.
        /// </summary>
        public sealed override void WriteEnd()
        {
            this.VerifyCanWriteEnd(true /*synchronousCall*/);
            this.InterceptException((thisParam) => thisParam.WriteEndImplementation());
            if (this.State == ParameterWriterState.Completed)
            {
                // Note that we intentionally go through the public API so that if the Flush fails the writer moves to the Error state.
                this.Flush();
            }
        }

        /// <summary>
        /// Asynchronously finish writing a parameter payload.
        /// </summary>
        /// <returns>A task instance that represents the asynchronous write operation.</returns>
        public sealed override Task WriteEndAsync()
        {
            this.VerifyCanWriteEnd(false /*synchronousCall*/);
            return this.InterceptExceptionAsync(
                async (thisParam) =>
                {
                    await thisParam.WriteEndImplementationAsync()
                        .ConfigureAwait(false);

                    if (thisParam.State == ParameterWriterState.Completed)
                    {
                        // Note that we intentionally go through the public API so that if the FlushAsync fails the writer moves to the Error state.
                        await thisParam.FlushAsync()
                            .ConfigureAwait(false);
                    }
                });
        }

        /// <summary>
        /// This method notifies the implementer of this interface that the created reader is in Exception state.
        /// </summary>
        void IODataReaderWriterListener.OnException()
        {
            Debug.Assert(this.State == ParameterWriterState.ActiveSubWriter, "this.State == ParameterWriterState.ActiveSubWriter");
            this.ReplaceScope(ParameterWriterState.Error);
        }

        /// <summary>
        /// This method notifies the implementer of this interface that the created reader is in Completed state.
        /// </summary>
        void IODataReaderWriterListener.OnCompleted()
        {
            Debug.Assert(this.State == ParameterWriterState.ActiveSubWriter, "this.State == ParameterWriterState.ActiveSubWriter");
            this.ReplaceScope(ParameterWriterState.CanWriteParameter);
        }

        /// <summary>
        /// This method notifies the listener, that an in-stream error is to be written.
        /// </summary>
        /// <remarks>
        /// This listener can choose to fail, if the currently written payload doesn't support in-stream error at this position.
        /// If the listener returns, the writer should not allow any more writing, since the in-stream error is the last thing in the payload.
        /// </remarks>
        void IODataOutputInStreamErrorListener.OnInStreamError()
        {
            // The parameter payload is written by the client and read by the server, we do not support
            // writing an in-stream error payload in this scenario.
            throw new ODataException(SRResources.ODataParameterWriter_InStreamErrorNotSupported);
        }

        /// <inheritdoc/>
        Task IODataOutputInStreamErrorListener.OnInStreamErrorAsync()
        {
            // The parameter payload is written by the client and read by the server, we do not support
            // writing an in-stream error payload in this scenario.
            throw new ODataException(SRResources.ODataParameterWriter_InStreamErrorNotSupported);
        }

        /// <summary>
        /// Check if the object has been disposed; called from all public API methods. Throws an ObjectDisposedException if the object
        /// has already been disposed.
        /// </summary>
        protected abstract void VerifyNotDisposed();

        /// <summary>
        /// Flush the output.
        /// </summary>
        protected abstract void FlushSynchronously();


        /// <summary>
        /// Flush the output.
        /// </summary>
        /// <returns>Task representing the pending flush operation.</returns>
        protected abstract Task FlushAsynchronously();

        /// <summary>
        /// Start writing an OData payload.
        /// </summary>
        protected abstract void StartPayload();

        /// <summary>
        /// Writes a value parameter (either primitive or complex).
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="parameterValue">The value of the parameter to write.</param>
        /// <param name="expectedTypeReference">The expected type reference of the parameter value.</param>
        protected abstract void WriteValueParameter(string parameterName, object parameterValue, IEdmTypeReference expectedTypeReference);

        /// <summary>
        /// Creates a format specific <see cref="ODataCollectionWriter"/> to write the value of a collection parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>The newly created <see cref="ODataCollectionWriter"/>.</returns>
        protected abstract ODataCollectionWriter CreateFormatCollectionWriter(string parameterName, IEdmTypeReference expectedItemType);

        /// <summary>Creates a format specific <see cref="ODataWriter"/> to write a resource.</summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>The newly created <see cref="ODataWriter"/>.</returns>
        protected abstract ODataWriter CreateFormatResourceWriter(string parameterName, IEdmTypeReference expectedItemType);

        /// <summary>Creates a format specific <see cref="ODataWriter"/> to write a resource set.</summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>The newly created <see cref="ODataWriter"/>.</returns>
        protected abstract ODataWriter CreateFormatResourceSetWriter(string parameterName, IEdmTypeReference expectedItemType);

        /// <summary>
        /// Finish writing an OData payload.
        /// </summary>
        protected abstract void EndPayload();

        /// <summary>
        /// Asynchronously start writing an OData payload.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        protected abstract Task StartPayloadAsync();

        /// <summary>
        /// Asynchronously finish writing an OData payload.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        protected abstract Task EndPayloadAsync();

        /// <summary>
        /// Asynchronously writes a value parameter (either primitive or complex).
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="parameterValue">The value of the parameter to write.</param>
        /// <param name="expectedTypeReference">The expected type reference of the parameter value.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        protected abstract Task WriteValueParameterAsync(string parameterName, object parameterValue, IEdmTypeReference expectedTypeReference);

        /// <summary>
        /// Asynchronously creates a format specific <see cref="ODataCollectionWriter"/> to write the value of a collection parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The value of the TResult parameter contains the newly created <see cref="ODataCollectionWriter"/>.</returns>
        protected abstract Task<ODataCollectionWriter> CreateFormatCollectionWriterAsync(string parameterName, IEdmTypeReference expectedItemType);

        /// <summary>
        /// Asynchronously creates a format specific <see cref="ODataWriter"/> to write a resource.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The value of the TResult parameter contains the newly created <see cref="ODataWriter"/>.</returns>
        protected abstract Task<ODataWriter> CreateFormatResourceWriterAsync(string parameterName, IEdmTypeReference expectedItemType);

        /// <summary>
        /// Asynchronously creates a format specific <see cref="ODataWriter"/> to write a resource set.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The value of the TResult parameter contains the newly created <see cref="ODataWriter"/>.</returns>
        protected abstract Task<ODataWriter> CreateFormatResourceSetWriterAsync(string parameterName, IEdmTypeReference expectedItemType);

        /// <summary>
        /// Verifies that calling WriteStart is valid.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        private void VerifyCanWriteStart(bool synchronousCall)
        {
            this.VerifyNotDisposed();
            this.VerifyCallAllowed(synchronousCall);
            if (this.State != ParameterWriterState.Start)
            {
                throw new ODataException(SRResources.ODataParameterWriterCore_CannotWriteStart);
            }
        }

        /// <summary>
        /// Start writing a parameter payload - implementation of the actual functionality.
        /// </summary>
        private void WriteStartImplementation()
        {
            Debug.Assert(this.State == ParameterWriterState.Start, "this.State == ParameterWriterState.Start");
            this.InterceptException((thisParam) => thisParam.StartPayload());
            this.EnterScope(ParameterWriterState.CanWriteParameter);
        }

        /// <summary>
        /// Verifies that the parameter with name <paramref name="parameterName"/> can be written and returns the
        /// type reference of the parameter.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        /// <param name="parameterName">The name of the parameter to be written.</param>
        /// <returns>The type reference of the parameter; null if no operation import was specified to the writer.</returns>
        private IEdmTypeReference VerifyCanWriteParameterAndGetTypeReference(bool synchronousCall, string parameterName)
        {
            Debug.Assert(!string.IsNullOrEmpty(parameterName), "!string.IsNullOrEmpty(parameterName)");
            this.VerifyNotDisposed();
            this.VerifyCallAllowed(synchronousCall);
            this.VerifyNotInErrorOrCompletedState();
            if (this.State != ParameterWriterState.CanWriteParameter)
            {
                throw new ODataException(SRResources.ODataParameterWriterCore_CannotWriteParameter);
            }

            if (this.parameterNamesWritten.Contains(parameterName))
            {
                throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_DuplicatedParameterNameNotAllowed, parameterName));
            }

            this.parameterNamesWritten.Add(parameterName);
            return this.GetParameterTypeReference(parameterName);
        }

        /// <summary>
        /// Verify that calling WriteValue is valid.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        /// <param name="parameterName">The name of the parameter to be written.</param>
        /// <param name="parameterValue">The value of the parameter to write.</param>
        /// <returns>The type reference of the parameter; null if no operation import was specified to the writer.</returns>
        private IEdmTypeReference VerifyCanWriteValueParameter(bool synchronousCall, string parameterName, object parameterValue)
        {
            Debug.Assert(!string.IsNullOrEmpty(parameterName), "!string.IsNullOrEmpty(parameterName)");
            IEdmTypeReference parameterTypeReference = this.VerifyCanWriteParameterAndGetTypeReference(synchronousCall, parameterName);
            if (parameterTypeReference != null && !parameterTypeReference.IsODataPrimitiveTypeKind() && !parameterTypeReference.IsODataEnumTypeKind() && !parameterTypeReference.IsODataTypeDefinitionTypeKind())
            {
                throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_CannotWriteValueOnNonValueTypeKind, parameterName, parameterTypeReference.TypeKind()));
            }

            if (parameterValue != null && (!EdmLibraryExtensions.IsPrimitiveType(parameterValue.GetType()) || parameterValue is Stream) && !(parameterValue is ODataEnumValue))
            {
                throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_CannotWriteValueOnNonSupportedValueType, parameterName, parameterValue.GetType()));
            }

            return parameterTypeReference;
        }

        /// <summary>
        /// Verify that calling CreateCollectionWriter is valid.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        /// <param name="parameterName">The name of the parameter to be written.</param>
        /// <returns>The expected item type of the items in the collection or null if no item type is available.</returns>
        private IEdmTypeReference VerifyCanCreateCollectionWriter(bool synchronousCall, string parameterName)
        {
            Debug.Assert(!string.IsNullOrEmpty(parameterName), "!string.IsNullOrEmpty(parameterName)");
            IEdmTypeReference parameterTypeReference = this.VerifyCanWriteParameterAndGetTypeReference(synchronousCall, parameterName);

            // TODO : Change to structureds Collection check
            if (parameterTypeReference != null && !parameterTypeReference.IsNonEntityCollectionType())
            {
                throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_CannotCreateCollectionWriterOnNonCollectionTypeKind, parameterName, parameterTypeReference.TypeKind()));
            }

            return parameterTypeReference == null ? null : parameterTypeReference.GetCollectionItemType();
        }

        /// <summary>
        /// Verify that calling CreateResourceWriter is valid.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        /// <param name="parameterName">The name of the parameter to be written.</param>
        /// <returns>The expected item type of the resource or null.</returns>
        private IEdmTypeReference VerifyCanCreateResourceWriter(bool synchronousCall, string parameterName)
        {
            Debug.Assert(!string.IsNullOrEmpty(parameterName), "!string.IsNullOrEmpty(parameterName)");
            IEdmTypeReference parameterTypeReference = this.VerifyCanWriteParameterAndGetTypeReference(synchronousCall, parameterName);
            if (parameterTypeReference != null && !parameterTypeReference.IsStructured())
            {
                throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_CannotCreateResourceWriterOnNonEntityOrComplexTypeKind, parameterName, parameterTypeReference.TypeKind()));
            }

            return parameterTypeReference;
        }

        /// <summary>
        /// Verify that calling CreateResourceSetWriter is valid.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        /// <param name="parameterName">The name of the parameter to be written.</param>
        /// <returns>The expected item type of the item in resource set or null.</returns>
        private IEdmTypeReference VerifyCanCreateResourceSetWriter(bool synchronousCall, string parameterName)
        {
            Debug.Assert(!string.IsNullOrEmpty(parameterName), "!string.IsNullOrEmpty(parameterName)");
            IEdmTypeReference parameterTypeReference = this.VerifyCanWriteParameterAndGetTypeReference(synchronousCall, parameterName);
            if (parameterTypeReference != null && !parameterTypeReference.IsStructuredCollectionType())
            {
                throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_CannotCreateResourceSetWriterOnNonStructuredCollectionTypeKind, parameterName, parameterTypeReference.TypeKind()));
            }

            return parameterTypeReference;
        }

        /// <summary>
        /// Gets the type reference of the parameter in question. Returns null if no operation import was specified to the writer.
        /// </summary>
        /// <param name="parameterName">The name of the parameter in question.</param>
        /// <returns>The type reference of the parameter; null if no operation import was specified to the writer.</returns>
        private IEdmTypeReference GetParameterTypeReference(string parameterName)
        {
            if (this.operation != null)
            {
                IEdmOperationParameter parameter = this.operation.FindParameter(parameterName);
                if (parameter == null)
                {
                    throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_ParameterNameNotFoundInOperation, parameterName, this.operation.Name));
                }

                return this.outputContext.EdmTypeResolver.GetParameterType(parameter);
            }

            return null;
        }

        /// <summary>
        /// Write a value parameter - implementation of the actual functionality.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="parameterValue">The value of the parameter to write (null/ODataEnumValue/primitiveClrValue).</param>
        /// <param name="expectedTypeReference">The expected type reference of the parameter value.</param>
        private void WriteValueImplementation(string parameterName, object parameterValue, IEdmTypeReference expectedTypeReference)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");
            this.InterceptException(
                (thisParam, parameterNameParam, parameterValueParam, expectedTypeReferenceParam) =>
                    thisParam.WriteValueParameter(parameterName, parameterValue, expectedTypeReference),
                parameterName, parameterValue, expectedTypeReference);
        }

        /// <summary>
        /// Creates an <see cref="ODataCollectionWriter"/> to write the value of a collection parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>The newly created <see cref="ODataCollectionWriter"/>.</returns>
        private ODataCollectionWriter CreateCollectionWriterImplementation(string parameterName, IEdmTypeReference expectedItemType)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");
            ODataCollectionWriter collectionWriter = this.CreateFormatCollectionWriter(parameterName, expectedItemType);
            this.ReplaceScope(ParameterWriterState.ActiveSubWriter);
            return collectionWriter;
        }

        /// <summary>
        /// Creates an <see cref="ODataWriter"/> to write a resource parameter.
        /// </summary>
        /// <param name="parameterName">The name of the  parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>The newly created <see cref="ODataWriter"/>.</returns>
        private ODataWriter CreateResourceWriterImplementation(string parameterName, IEdmTypeReference expectedItemType)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");
            ODataWriter resourceWriter = this.CreateFormatResourceWriter(parameterName, expectedItemType);
            this.ReplaceScope(ParameterWriterState.ActiveSubWriter);
            return resourceWriter;
        }

        /// <summary>
        /// Creates an <see cref="ODataWriter"/> to write a resource set parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>The newly created <see cref="ODataCollectionWriter"/>.</returns>
        private ODataWriter CreateResourceSetWriterImplementation(string parameterName, IEdmTypeReference expectedItemType)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");
            ODataWriter resourceSetWriter = this.CreateFormatResourceSetWriter(parameterName, expectedItemType);
            this.ReplaceScope(ParameterWriterState.ActiveSubWriter);
            return resourceSetWriter;
        }

        /// <summary>
        /// Verifies that calling WriteEnd is valid.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        private void VerifyCanWriteEnd(bool synchronousCall)
        {
            this.VerifyNotDisposed();
            this.VerifyCallAllowed(synchronousCall);
            this.VerifyNotInErrorOrCompletedState();
            if (this.State != ParameterWriterState.CanWriteParameter)
            {
                throw new ODataException(SRResources.ODataParameterWriterCore_CannotWriteEnd);
            }

            this.VerifyAllParametersWritten();
        }

        /// <summary>
        /// If an <see cref="IEdmOperationImport"/> is specified, then this method ensures that all parameters present in the
        /// operation import are written to the payload.
        /// </summary>
        /// <remarks>The binding parameter is optional in the payload. Hence this method will not check for missing binding parameter.</remarks>
        private void VerifyAllParametersWritten()
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");

            if (this.operation != null && this.operation.Parameters != null)
            {
                IEnumerable<IEdmOperationParameter> parameters = null;
                if (this.operation.IsBound)
                {
                    // The binding parameter may or may not be present in the payload. Hence we don't throw error if the binding parameter is missing.
                    parameters = this.operation.Parameters.Skip(1);
                }
                else
                {
                    parameters = this.operation.Parameters;
                }

                IEnumerable<string> missingParameters = parameters.Where(p => !this.parameterNamesWritten.Contains(p.Name) && !this.outputContext.EdmTypeResolver.GetParameterType(p).IsNullable).Select(p => p.Name);
                if (missingParameters.Any())
                {
                    missingParameters = missingParameters.Select(name => String.Format(CultureInfo.InvariantCulture, "'{0}'", name));

                    // We're calling the ToArray here since not all platforms support the string.Join which takes IEnumerable.
                    throw new ODataException(Error.Format(SRResources.ODataParameterWriterCore_MissingParameterInParameterPayload, string.Join(", ", missingParameters.ToArray()), this.operation.Name));
                }
            }
        }

        /// <summary>
        /// Finish writing a parameter payload - implementation of the actual functionality.
        /// </summary>
        private void WriteEndImplementation()
        {
            this.InterceptException((thisParam) => thisParam.EndPayload());
            this.LeaveScope();
        }

        /// <summary>
        /// Verifies that the current state is not Error or Completed.
        /// </summary>
        private void VerifyNotInErrorOrCompletedState()
        {
            if (this.State == ParameterWriterState.Error || this.State == ParameterWriterState.Completed)
            {
                throw new ODataException(SRResources.ODataParameterWriterCore_CannotWriteInErrorOrCompletedState);
            }
        }

        /// <summary>
        /// Verifies that calling Flush is valid.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        private void VerifyCanFlush(bool synchronousCall)
        {
            this.VerifyNotDisposed();
            this.VerifyCallAllowed(synchronousCall);
        }

        /// <summary>
        /// Verifies that a call is allowed to the writer.
        /// </summary>
        /// <param name="synchronousCall">true if the call is to be synchronous; false otherwise.</param>
        private void VerifyCallAllowed(bool synchronousCall)
        {
            if (synchronousCall)
            {
                if (!this.outputContext.Synchronous)
                {
                    throw new ODataException(SRResources.ODataParameterWriterCore_SyncCallOnAsyncWriter);
                }
            }
            else
            {
                if (this.outputContext.Synchronous)
                {
                    throw new ODataException(SRResources.ODataParameterWriterCore_AsyncCallOnSyncWriter);
                }
            }
        }

        /// <summary>
        /// Catch any exception thrown by the action passed in; in the exception case move the writer into
        /// state Error and then rethrow the exception.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <remarks>
        /// Make sure to only use anonymous functions that don't capture state from the enclosing context, 
        /// so the compiler optimizes the code to avoid delegate and closure allocations on every call to this method.
        /// </remarks>
        private void InterceptException(Action<ODataParameterWriterCore> action)
        {
            try
            {
                action(this);
            }
            catch
            {
                this.EnterErrorScope();

                throw;
            }
        }

        /// <summary>
        /// Catch any exception thrown by the action passed in; in the exception case move the writer into
        /// state Error and then rethrow the exception.
        /// </summary>
        /// <typeparam name="TArg0">The delegate first argument type.</typeparam>
        /// <typeparam name="TArg1">The delegate second argument type.</typeparam>
        /// <typeparam name="TArg2">The delegate third argument type.</typeparam>
        /// <param name="action">The action to execute.</param>
        /// <param name="arg0">The first argument value provided to the action.</param>
        /// <param name="arg1">The second argument value provided to the action.</param>
        /// <param name="arg2">The third argument value provided to the action.</param>
        /// <remarks>
        /// Make sure to only use anonymous functions that don't capture state from the enclosing context, 
        /// so the compiler optimizes the code to avoid delegate and closure allocations on every call to this method.
        /// </remarks>
        private void InterceptException<TArg0, TArg1, TArg2>(Action<ODataParameterWriterCore, TArg0, TArg1, TArg2> action, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            try
            {
                action(this, arg0, arg1, arg2);
            }
            catch
            {
                this.EnterErrorScope();

                throw;
            }
        }

        /// <summary>
        /// Catch any exception thrown by the function passed in; in the exception case move the writer into
        /// state Error and then rethrow the exception.
        /// </summary>
        /// <typeparam name="T">The return type of <paramref name="function"/>.</typeparam>
        /// <typeparam name="TArg0">The delegate first argument type.</typeparam>
        /// <typeparam name="TArg1">The delegate second argument type.</typeparam>
        /// <param name="function">The function to execute.</param>
        /// <param name="arg0">The first argument value provided to the action.</param>
        /// <param name="arg1">The second argument value provided to the action.</param>
        /// <returns>Returns the return value from executing <paramref name="function"/>.</returns>
        /// <remarks>
        /// Make sure to only use anonymous functions that don't capture state from the enclosing context, 
        /// so the compiler optimizes the code to avoid delegate and closure allocations on every call to this method.
        /// </remarks>
        private T InterceptException<T, TArg0, TArg1>(Func<ODataParameterWriterCore, TArg0, TArg1, T> function, TArg0 arg0, TArg1 arg1)
        {
            try
            {
                return function(this, arg0, arg1);
            }
            catch
            {
                this.EnterErrorScope();

                throw;
            }
        }

        /// <summary>
        /// Enters the Error scope if we are not already in Error state.
        /// </summary>
        private void EnterErrorScope()
        {
            if (this.State != ParameterWriterState.Error)
            {
                this.EnterScope(ParameterWriterState.Error);
            }
        }

        /// <summary>
        /// Verifies that the transition from the current state into new state is valid and enter a new writer scope.
        /// </summary>
        /// <param name="newState">The writer state to transition into.</param>
        private void EnterScope(ParameterWriterState newState)
        {
            this.ValidateTransition(newState);
            this.scopes.Push(newState);
        }

        /// <summary>
        /// Leave the current writer scope and return to the previous scope.
        /// When reaching the top-level replace the 'Start' scope with a 'Completed' scope.
        /// </summary>
        /// <remarks>Note that this method is never called once the writer is in 'Error' state.</remarks>
        private void LeaveScope()
        {
            Debug.Assert(this.State != ParameterWriterState.Error, "this.State != WriterState.Error");
            this.ValidateTransition(ParameterWriterState.Completed);

            // scopes is either [Start, CanWriteParameter] or [Start]
            if (this.State == ParameterWriterState.CanWriteParameter)
            {
                this.scopes.Pop();
            }

            Debug.Assert(this.State == ParameterWriterState.Start, "this.State == ParameterWriterState.Start");
            this.ReplaceScope(ParameterWriterState.Completed);
        }

        /// <summary>
        /// Replaces the current scope with a new scope; checks that the transition is valid.
        /// </summary>
        /// <param name="newState">The new state to transition into.</param>
        private void ReplaceScope(ParameterWriterState newState)
        {
            this.ValidateTransition(newState);
            this.scopes.Pop();
            this.scopes.Push(newState);
        }

        /// <summary>
        /// Verify that the transition from the current state into new state is valid.
        /// </summary>
        /// <param name="newState">The new writer state to transition into.</param>
        private void ValidateTransition(ParameterWriterState newState)
        {
            if (this.State != ParameterWriterState.Error && newState == ParameterWriterState.Error)
            {
                // we can always transition into an error state if we are not already in an error state
                return;
            }

            switch (this.State)
            {
                case ParameterWriterState.Start:
                    if (newState != ParameterWriterState.CanWriteParameter && newState != ParameterWriterState.Completed)
                    {
                        throw new ODataException(Error.Format(SRResources.General_InternalError, InternalErrorCodes.ODataParameterWriterCore_ValidateTransition_InvalidTransitionFromStart));
                    }

                    break;
                case ParameterWriterState.CanWriteParameter:
                    if (newState != ParameterWriterState.CanWriteParameter && newState != ParameterWriterState.ActiveSubWriter && newState != ParameterWriterState.Completed)
                    {
                        throw new ODataException(Error.Format(SRResources.General_InternalError, InternalErrorCodes.ODataParameterWriterCore_ValidateTransition_InvalidTransitionFromCanWriteParameter));
                    }

                    break;
                case ParameterWriterState.ActiveSubWriter:
                    if (newState != ParameterWriterState.CanWriteParameter)
                    {
                        throw new ODataException(Error.Format(SRResources.General_InternalError, InternalErrorCodes.ODataParameterWriterCore_ValidateTransition_InvalidTransitionFromActiveSubWriter));
                    }

                    break;
                case ParameterWriterState.Completed:
                    // we should never see a state transition when in state 'Completed'
                    throw new ODataException(Error.Format(SRResources.General_InternalError, InternalErrorCodes.ODataParameterWriterCore_ValidateTransition_InvalidTransitionFromCompleted));
                case ParameterWriterState.Error:
                    if (newState != ParameterWriterState.Error)
                    {
                        // No more state transitions once we are in error state
                        throw new ODataException(Error.Format(SRResources.General_InternalError, InternalErrorCodes.ODataParameterWriterCore_ValidateTransition_InvalidTransitionFromError));
                    }

                    break;
                default:
                    throw new ODataException(Error.Format(SRResources.General_InternalError, InternalErrorCodes.ODataParameterWriterCore_ValidateTransition_UnreachableCodePath));
            }
        }

        /// <summary>
        /// Asynchronously catch any exception thrown by the action passed in; in the exception case move the writer into
        /// state Error and then rethrow the exception.
        /// </summary>
        /// <param name="func">The delegate to execute asynchronously.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// Make sure to only use anonymous functions that don't capture state from the enclosing context, 
        /// so the compiler optimizes the code to avoid delegate and closure allocations on every call to this method.
        /// </remarks>
        private async Task InterceptExceptionAsync(Func<ODataParameterWriterCore, Task> func)
        {
            try
            {
                await func(this).ConfigureAwait(false);
            }
            catch
            {
                this.EnterErrorScope();

                throw;
            }
        }

        /// <summary>
        /// Asynchronously catch any exception thrown by the action passed in; in the exception case move the writer into
        /// state Error and then rethrow the exception.
        /// </summary>
        /// <typeparam name="TArg1">The <paramref name="func"/> delegate first argument type.</typeparam>
        /// <typeparam name="TArg2">The <paramref name="func"/> delegate second argument type.</typeparam>
        /// <typeparam name="TArg3">The <paramref name="func"/> delegate third argument type.</typeparam>
        /// <param name="func">The delegate to execute asynchronously.</param>
        /// <param name="arg1">The first argument value provided to the <paramref name="func"/> delegate.</param>
        /// <param name="arg2">The second argument value provided to the <paramref name="func"/> delegate.</param>
        /// <param name="arg3">The third argument value provided to the <paramref name="func"/> delegate.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// Make sure to only use anonymous functions that don't capture state from the enclosing context, 
        /// so the compiler optimizes the code to avoid delegate and closure allocations on every call to this method.
        /// </remarks>
        private async Task InterceptExceptionAsync<TArg1, TArg2, TArg3>(
            Func<ODataParameterWriterCore, TArg1, TArg2, TArg3, Task> func,
            TArg1 arg1,
            TArg2 arg2,
            TArg3 arg3)
        {
            try
            {
                await func(this, arg1, arg2, arg3).ConfigureAwait(false);
            }
            catch
            {
                this.EnterErrorScope();

                throw;
            }
        }

        /// <summary>
        /// Asynchronously catch any exception thrown by the action passed in; in the exception case move the writer into
        /// state Error and then rethrow the exception.
        /// </summary>
        /// <typeparam name="T">The type of the result returned by the <paramref name="func"/> delegate.</typeparam>
        /// <typeparam name="TArg1">The <paramref name="func"/> delegate first argument type.</typeparam>
        /// <typeparam name="TArg2">The <paramref name="func"/> delegate second argument type.</typeparam>
        /// <param name="func">The delegate to execute asynchronously.</param>
        /// <param name="arg1">The first argument value provided to the <paramref name="func"/> delegate.</param>
        /// <param name="arg2">The second argument value provided to the <paramref name="func"/> delegate.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The value of the TResult parameter contains a T instance.</returns>
        /// <remarks>
        /// Make sure to only use anonymous functions that don't capture state from the enclosing context, 
        /// so the compiler optimizes the code to avoid delegate and closure allocations on every call to this method.
        /// </remarks>
        private async Task<T> InterceptExceptionAsync<T, TArg1, TArg2>(
            Func<ODataParameterWriterCore, TArg1, TArg2, Task<T>> func,
            TArg1 arg1,
            TArg2 arg2)
        {
            try
            {
                return await func(this, arg1, arg2).ConfigureAwait(false);
            }
            catch
            {
                this.EnterErrorScope();

                throw;
            }
        }

        /// <summary>
        /// Catch any exception thrown by the action passed in; in the exception case move the reader into
        /// state Exception and then rethrow the exception.
        /// </summary>
        /// <typeparam name="TResult">The type returned from the <paramref name="action"/></typeparam>
        /// <param name="action">The action to execute.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The value of the TResult parameter contains the result of executing the <paramref name="action"/>.
        /// </returns>
        private async Task<TResult> InterceptExceptionAsync<TResult>(Func<ODataParameterWriterCore, Task<TResult>> action)
        {
            try
            {
                return await action(this).ConfigureAwait(false);
            }
            catch
            {
                this.EnterErrorScope();

                throw;
            }
        }

        /// <summary>
        /// Asynchronously start writing a parameter payload - implementation of the actual functionality.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        private async Task WriteStartImplementationAsync()
        {
            Debug.Assert(this.State == ParameterWriterState.Start, "this.State == ParameterWriterState.Start");

            await this.InterceptExceptionAsync((thisParam) => thisParam.StartPayloadAsync())
                .ConfigureAwait(false);
            this.EnterScope(ParameterWriterState.CanWriteParameter);
        }

        /// <summary>
        /// Asynchronously write a value parameter - implementation of the actual functionality.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to write.</param>
        /// <param name="parameterValue">The value of the parameter to write (null/ODataEnumValue/primitiveClrValue).</param>
        /// <param name="expectedTypeReference">The expected type reference of the parameter value.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        private Task WriteValueImplementationAsync(string parameterName, object parameterValue, IEdmTypeReference expectedTypeReference)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");

            return this.InterceptExceptionAsync((
                thisParam,
                parameterNameParam,
                parameterValueParam,
                expectedTypeReferenceParam) => thisParam.WriteValueParameterAsync(
                    parameterNameParam,
                    parameterValueParam,
                    expectedTypeReferenceParam),
                parameterName,
                parameterValue,
                expectedTypeReference);
        }

        /// <summary>
        /// Asynchronously creates an <see cref="ODataCollectionWriter"/> to write the value of a collection parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The value of the TResult parameter contains the newly created <see cref="ODataCollectionWriter"/>.</returns>
        private async Task<ODataCollectionWriter> CreateCollectionWriterImplementationAsync(string parameterName, IEdmTypeReference expectedItemType)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");

            ODataCollectionWriter collectionWriter = await this.CreateFormatCollectionWriterAsync(parameterName, expectedItemType)
                .ConfigureAwait(false);
            this.ReplaceScope(ParameterWriterState.ActiveSubWriter);
            return collectionWriter;
        }

        /// <summary>
        /// Asynchronously creates an <see cref="ODataWriter"/> to write a resource parameter.
        /// </summary>
        /// <param name="parameterName">The name of the  parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The value of the TResult parameter contains the newly created <see cref="ODataWriter"/>.</returns>
        private async Task<ODataWriter> CreateResourceWriterImplementationAsync(string parameterName, IEdmTypeReference expectedItemType)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");

            ODataWriter resourceWriter = await this.CreateFormatResourceWriterAsync(parameterName, expectedItemType)
                .ConfigureAwait(false);
            this.ReplaceScope(ParameterWriterState.ActiveSubWriter);

            return resourceWriter;
        }

        /// <summary>
        /// Asynchronously creates an <see cref="ODataWriter"/> to write a resource set parameter.
        /// </summary>
        /// <param name="parameterName">The name of the collection parameter to write.</param>
        /// <param name="expectedItemType">The type reference of the expected item type or null if no expected item type exists.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The value of the TResult parameter contains the newly created <see cref="ODataWriter"/>.</returns>
        private async Task<ODataWriter> CreateResourceSetWriterImplementationAsync(string parameterName, IEdmTypeReference expectedItemType)
        {
            Debug.Assert(this.State == ParameterWriterState.CanWriteParameter, "this.State == ParameterWriterState.CanWriteParameter");

            ODataWriter resourceSetWriter = await this.CreateFormatResourceSetWriterAsync(parameterName, expectedItemType)
                .ConfigureAwait(false);
            this.ReplaceScope(ParameterWriterState.ActiveSubWriter);

            return resourceSetWriter;
        }

        /// <summary>
        /// Asynchronously finish writing a parameter payload - implementation of the actual functionality.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        private async Task WriteEndImplementationAsync()
        {
            await this.InterceptExceptionAsync((thisParam) => thisParam.EndPayloadAsync())
                .ConfigureAwait(false);
            this.LeaveScope();
        }
    }
}
