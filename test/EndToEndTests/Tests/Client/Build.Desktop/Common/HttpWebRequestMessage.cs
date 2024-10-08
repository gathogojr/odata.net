﻿//---------------------------------------------------------------------
// <copyright file="HttpWebRequestMessage.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.Test.OData.Tests.Client.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.OData;

    /// An implementation of IODataRequestMessageAsync that uses an HttpWebRequest under the covers.
    /// In ODataLibrary, a message is an abstraction which consists of stream and header interfaces that hides the details of stream-reading/writing.
    public class HttpWebRequestMessage : IODataRequestMessageAsync, IServiceCollectionProvider
    {
        private readonly HttpWebRequest request;
        private bool lockedHeaders = false;

        public HttpWebRequestMessage(Uri uri)
        {
            request = (HttpWebRequest)WebRequest.Create(uri);
        }

        public string GetHeader(string headerName)
        {
            return request.Headers.Get(headerName);
        }

        public Task<Stream> GetStreamAsync()
        {
            lockedHeaders = true;

            TaskCompletionSource<Stream> completionSource = new TaskCompletionSource<Stream>();
            completionSource.SetResult(request.GetRequestStream());
            return completionSource.Task;
        }

        public IEnumerable<KeyValuePair<string, string>> Headers
        {
            get
            {
                foreach (string headerName in this.request.Headers.Keys)
                {
                    yield return new KeyValuePair<string, string>(headerName, this.request.Headers[headerName]);
                }
            }
        }

        public void SetHeader(string headerName, string headerValue)
        {
            if (lockedHeaders)
            {
                throw new InvalidOperationException("Cannot set headers they have already been written to the stream");
            }

            if (headerName == "Content-Type")
            {
                request.ContentType = headerValue;
            }
            else if (headerName == "Accept")
            {
                request.Accept = headerValue;
            }
            else
            {
                request.Headers.Add(headerName, headerValue);
            }
        }

        public IODataResponseMessage GetResponse()
        {
            WebResponse response;
            try
            {
                response = request.GetResponse();
            }
            catch (WebException webException)
            {
                //var msg = "testing testing ..";
                //var msg = "007msg ---  " + request.RequestUri + "  " + webException.Message + " " + webException.StackTrace + " " + webException.InnerException ?? "";
                //throw new Exception(msg);

                if (webException.Response == null)
                {
                    throw;
                }

                response = webException.Response;
            }

            return new HttpWebResponseMessage((HttpWebResponse)response)
            {
                ServiceProvider = this.ServiceProvider
            };
        }

        public Stream GetStream()
        {
            return request.GetRequestStream();
        }

        public Uri Url
        {
            get 
            { 
                return request.RequestUri; 
            }

            set 
            {
                throw new InvalidOperationException("Request Uri cannot be changed"); 
            }
        }

        public string Method
        {
            get 
            { 
                return request.Method; 
            }

            set 
            { 
                request.Method = value; 
            }
        }

        public IServiceProvider ServiceProvider { get; set; }
    }
}

