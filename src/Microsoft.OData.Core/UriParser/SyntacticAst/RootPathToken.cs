﻿//---------------------------------------------------------------------
// <copyright file="EndPathToken.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

using System.Collections.Generic;
#if ODATA_CLIENT
namespace Microsoft.OData.Client.ALinq.UriParser
#else
namespace Microsoft.OData.UriParser
#endif
{
    /// <summary>
    /// Lexical token representing a $root path.
    /// </summary>
    public sealed class RootPathToken : QueryToken
    {
        /// <summary>
        /// The kind of the query token.
        /// </summary>
        public override QueryTokenKind Kind => QueryTokenKind.RootPath;

        /// <summary>
        /// Path segments (without the $root leading segment)
        /// </summary>
        public IList<string> Segments { get; } = new List<string>();

        /// <summary>
        /// Accept a <see cref="ISyntacticTreeVisitor{T}"/> to walk a tree of <see cref="QueryToken"/>s.
        /// </summary>
        /// <typeparam name="T">Type that the visitor will return after visiting this token.</typeparam>
        /// <param name="visitor">An implementation of the visitor interface.</param>
        /// <returns>An object whose type is determined by the type parameter of the visitor.</returns>
        public override T Accept<T>(ISyntacticTreeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}