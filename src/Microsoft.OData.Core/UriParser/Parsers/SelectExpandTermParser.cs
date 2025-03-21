﻿//---------------------------------------------------------------------
// <copyright file="SelectExpandTermParser.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.OData.UriParser
{
    using System;
    using Microsoft.OData.Core;

    /// <summary>
    /// Sub-parser that <see cref="SelectExpandParser"/> uses to parse a single select or expand term.
    /// Uses a provided Lexer, which must be positioned at the term, to parse the term.
    /// </summary>
    internal sealed class SelectExpandTermParser
    {
        /// <summary>
        /// Lexer provided by <see cref="SelectExpandParser"/>.
        /// </summary>
        private readonly ExpressionLexer lexer;

        /// <summary>
        /// Max length of a select or expand path.
        /// </summary>
        private readonly int maxPathLength;

        /// <summary>
        /// True if we are parsing select, false if we are parsing expand.
        /// </summary>
        private readonly bool isSelect;

        /// <summary>
        /// Constructs a term parser.
        /// </summary>
        /// <param name="lexer">Lexer to use for parsing the term. Should be position at the term to parse.</param>
        /// <param name="maxPathLength">Max length of a select or expand path.</param>
        /// <param name="isSelect">True if we are parsing select, false if we are parsing expand.</param>
        internal SelectExpandTermParser(ExpressionLexer lexer, int maxPathLength, bool isSelect)
        {
            this.lexer = lexer;
            this.maxPathLength = maxPathLength;
            this.isSelect = isSelect;
        }

        /// <summary>
        /// Parses a select or expand term into a PathSegmentToken.
        /// Assumes the lexer is positioned at the beginning of the term to parse.
        /// When done, the lexer will be positioned at whatever is after the identifier.
        /// </summary>
        /// <param name="allowRef">Whether the $ref operation is valid in this token.</param>
        /// <returns>parsed query token</returns>
        internal PathSegmentToken ParseTerm(bool allowRef = false)
        {
            int pathLength;
            PathSegmentToken token = this.ParseSegment(null, allowRef);
            if (token != null)
            {
                pathLength = 1;
            }
            else
            {
                return null;
            }

            this.CheckPathLength(pathLength);

            // If this property was a path, walk that path. e.g. SomeComplex/SomeInnerComplex/SomeNavProp
            while (this.lexer.CurrentToken.Kind == ExpressionTokenKind.Slash)
            {
                // Move from '/' to the next segment
                this.lexer.NextToken();

                // TODO: Could remove V4 if we don't want to allow a trailing '/' character
                // Allow a single trailing slash for backwards compatibility with the WCF DS Server parser.
                if (pathLength > 1 && this.lexer.CurrentToken.Kind == ExpressionTokenKind.End)
                {
                    break;
                }

                token = this.ParseSegment(token, allowRef);
                if (token != null)
                {
                    this.CheckPathLength(++pathLength);
                }
            }

            return token;
        }

        /// <summary>
        /// Check that the current path length is less than the maximum path length
        /// </summary>
        /// <param name="pathLength">the current path length</param>
        private void CheckPathLength(int pathLength)
        {
            if (pathLength > this.maxPathLength)
            {
                throw new ODataException(SRResources.UriQueryExpressionParser_TooDeep);
            }
        }

        /// <summary>
        /// Uses the ExpressionLexer to visit the next ExpressionToken, and delegates parsing of segments, type segments, identifiers,
        /// and the star token to other methods.
        /// </summary>
        /// <param name="previousSegment">Previously parsed PathSegmentToken, or null if this is the first token.</param>
        /// <param name="allowRef">Whether the $ref operation is valid in this token.</param>
        /// <returns>A parsed PathSegmentToken representing the next segment in this path.</returns>
        private PathSegmentToken ParseSegment(PathSegmentToken previousSegment, bool allowRef)
        {
            if (this.lexer.CurrentToken.Span.StartsWith("$", StringComparison.Ordinal)
                && (!allowRef || !this.lexer.CurrentToken.Span.Equals(UriQueryConstants.RefSegment, StringComparison.Ordinal))
                && !this.lexer.CurrentToken.Span.Equals(UriQueryConstants.CountSegment, StringComparison.Ordinal))
            {
                throw new ODataException(Error.Format(SRResources.UriSelectParser_SystemTokenInSelectExpand, this.lexer.CurrentToken.Text.ToString(), this.lexer.ExpressionText));
            }

            // Some check here to throw exception, prop1/*/prop2 and */$ref/prop and prop1/$count/prop2 will throw exception, all are $expand cases.
            if (!isSelect)
            {
                if (previousSegment != null && previousSegment.Identifier == UriQueryConstants.Star && !this.lexer.CurrentToken.GetIdentifier().Equals(UriQueryConstants.RefSegment, StringComparison.Ordinal))
                {
                    // Star can only be followed with $ref. $count is not supported with star as expand option
                    throw new ODataException(SRResources.ExpressionToken_OnlyRefAllowWithStarInExpand);
                }
                else if (previousSegment != null && previousSegment.Identifier == UriQueryConstants.RefSegment)
                {
                    // $ref should not have more property followed.
                    throw new ODataException(SRResources.ExpressionToken_NoPropAllowedAfterRef);
                }
                else if (previousSegment != null && previousSegment.Identifier == UriQueryConstants.CountSegment)
                {
                    // $count should not have more property followed. e.g $expand=NavProperty/$count/MyProperty
                    throw new ODataException(SRResources.ExpressionToken_NoPropAllowedAfterDollarCount);
                }
            }


            ReadOnlySpan<char> propertyName;

            if (this.lexer.PeekNextToken().Kind == ExpressionTokenKind.Dot)
            {
                propertyName = this.lexer.ReadDottedIdentifier(this.isSelect);
            }
            else if (this.lexer.CurrentToken.Kind == ExpressionTokenKind.Star)
            {
                // "*/$ref" is supported in expand
                if (this.lexer.PeekNextToken().Kind == ExpressionTokenKind.Slash && isSelect)
                {
                    throw new ODataException(Error.Format(SRResources.ExpressionToken_IdentifierExpected, this.lexer.Position));
                }
                else if (previousSegment != null && !isSelect)
                {
                    // expand option like "customer?$expand=VIPCustomer/*" is not allowed as specification does not allowed any property before *.
                    throw new ODataException(SRResources.ExpressionToken_NoSegmentAllowedBeforeStarInExpand);
                }

                propertyName = this.lexer.CurrentToken.Span;
                this.lexer.NextToken();
            }
            else
            {
                propertyName = this.lexer.CurrentToken.GetIdentifier();
                this.lexer.NextToken();
            }

            // By design, "/$count" or "/$ref" should return using SystemToken, but the existing implementation is using "NonSystemToken".
            // So far, let's keep it unchanged.
            return new NonSystemToken(propertyName.ToString(), null, previousSegment);
        }
    }
}