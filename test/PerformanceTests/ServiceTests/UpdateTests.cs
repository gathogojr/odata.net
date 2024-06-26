﻿//---------------------------------------------------------------------
// <copyright file="UpdateTests.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.OData.Performance
{
    using System;
    using global::Xunit;
    using Microsoft.OData;
    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// Performance tests for OData POST, PUT, PATCH and DELETE
    /// requests against a real (locally running) service.
    /// These tests involve writing the request payload and sending the
    /// request to the service as well as get the response status code.
    /// </summary>
    [MemoryDiagnoser]
    public class UpdateTests : IClassFixture<TestServiceFixture<UpdateTests>>
    {
        TestServiceFixture<UpdateTests> serviceFixture;

        private const string NameSpacePrefix = "Microsoft.Test.OData.Services.PerfService.";

        [GlobalSetup]
        public void SetupService()
        {
            serviceFixture = new TestServiceFixture<UpdateTests>();
        }

        [GlobalCleanup]
        public void KillService()
        {
            serviceFixture.Dispose();
        }

        [IterationSetup]
        public void ResetDataSource()
        {
            ODataMessageWriterSettings writerSettings = new ODataMessageWriterSettings() { BaseUri = serviceFixture.ServiceBaseUri };
            writerSettings.ODataUri = new ODataUri() { ServiceRoot = serviceFixture.ServiceBaseUri };
            var requestMessage = new HttpWebRequestMessage(new Uri(serviceFixture.ServiceBaseUri.AbsoluteUri + "ResetDataSource", UriKind.Absolute));
            requestMessage.Method = "POST";
            requestMessage.ContentLength = 0;
            var responseMessage = requestMessage.GetResponse();
            Assert.Equal(204, responseMessage.StatusCode);
        }

        ////[Benchmark]
        public void PostEntity()
        {
            int RequestsPerIteration = 100;
            int PersonIdBase = 100;

            for (int i = 0; i < RequestsPerIteration; i++)
            {
                ODataMessageWriterSettings writerSettings = new ODataMessageWriterSettings()
                {
                    BaseUri = serviceFixture.ServiceBaseUri
                };
                writerSettings.ODataUri = new ODataUri() { ServiceRoot = serviceFixture.ServiceBaseUri };
                var requestMessage =
                    new HttpWebRequestMessage(
                        new Uri(serviceFixture.ServiceBaseUri.AbsoluteUri + "SimplePeopleSet", UriKind.Absolute));
                requestMessage.Method = "POST";

                var peopleEntry = new ODataResource()
                {
                    EditLink = new Uri("/SimplePeopleSet(" + (PersonIdBase++) + ")", UriKind.Relative),
                    Id = new Uri("/SimplePeopleSet(" + PersonIdBase + ")", UriKind.Relative),
                    TypeName = NameSpacePrefix + "Person",
                    Properties = new[]
                    {
                                new ODataProperty {Name = "PersonID", Value = PersonIdBase},
                                new ODataProperty {Name = "FirstName", Value = "PostEntity"},
                                new ODataProperty {Name = "LastName", Value = "PostEntity"},
                                new ODataProperty {Name = "MiddleName", Value = "PostEntity"}
                            },
                };

                using (var messageWriter = new ODataMessageWriter(requestMessage, writerSettings))
                {
                    var odataWriter = messageWriter.CreateODataResourceWriter();
                    odataWriter.WriteStart(peopleEntry);
                    odataWriter.WriteEnd();
                }

                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(201, responseMessage.StatusCode);
            }
        }

        ////[Benchmark]
        public void DeleteEntity()
        {
            int RequestsPerIteration = 100;
            int PersonIdBase = 1;


            for (int i = 0; i < RequestsPerIteration; i++)
            {
                ODataMessageWriterSettings writerSettings = new ODataMessageWriterSettings() { BaseUri = serviceFixture.ServiceBaseUri };
                writerSettings.ODataUri = new ODataUri() { ServiceRoot = serviceFixture.ServiceBaseUri };
                var requestMessage = new HttpWebRequestMessage(new Uri(serviceFixture.ServiceBaseUri.AbsoluteUri + "SimplePeopleSet(" + (PersonIdBase++) + ")", UriKind.Absolute));
                requestMessage.Method = "DELETE";
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(204, responseMessage.StatusCode);
            }
        }

        ////[Benchmark]
        public void PutEntity()
        {
            int RequestsPerIteration = 100;
            int PersonIdBase = 1;

            for (int i = 0; i < RequestsPerIteration; i++)
            {
                ODataMessageWriterSettings writerSettings = new ODataMessageWriterSettings() { BaseUri = serviceFixture.ServiceBaseUri };
                writerSettings.ODataUri = new ODataUri() { ServiceRoot = serviceFixture.ServiceBaseUri };
                var requestMessage = new HttpWebRequestMessage(new Uri(serviceFixture.ServiceBaseUri.AbsoluteUri + "SimplePeopleSet(" + PersonIdBase + ")", UriKind.Absolute));
                requestMessage.Method = "PUT";

                var peopleEntry = new ODataResource()
                {
                    Properties = new[]
                {
                            new ODataProperty { Name = "PersonID", Value = PersonIdBase++ },
                            new ODataProperty { Name = "FirstName", Value = "PostEntity" },
                            new ODataProperty { Name = "LastName", Value = "PostEntity" },
                            new ODataProperty { Name = "MiddleName", Value = "NewMiddleName" }
                        },
                };

                using (var messageWriter = new ODataMessageWriter(requestMessage, writerSettings))
                {
                    var odataWriter = messageWriter.CreateODataResourceWriter();
                    odataWriter.WriteStart(peopleEntry);
                    odataWriter.WriteEnd();
                }
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(204, responseMessage.StatusCode);
            }
        }

        ////[Benchmark]
        public void PatchEntity()
        {
            int RequestsPerIteration = 100;
            int PersonIdBase = 1;

            for (int i = 0; i < RequestsPerIteration; i++)
            {
                ODataMessageWriterSettings writerSettings = new ODataMessageWriterSettings() { BaseUri = serviceFixture.ServiceBaseUri };
                writerSettings.ODataUri = new ODataUri() { ServiceRoot = serviceFixture.ServiceBaseUri };
                var requestMessage = new HttpWebRequestMessage(new Uri(serviceFixture.ServiceBaseUri.AbsoluteUri + "SimplePeopleSet(" + (PersonIdBase++) + ")", UriKind.Absolute));
                requestMessage.Method = "PATCH";

                var peopleEntry = new ODataResource()
                {
                    Properties = new[]
                {
                            new ODataProperty { Name = "MiddleName", Value = "NewMiddleName" }
                        },
                };

                using (var messageWriter = new ODataMessageWriter(requestMessage, writerSettings))
                {
                    var odataWriter = messageWriter.CreateODataResourceWriter();
                    odataWriter.WriteStart(peopleEntry);
                    odataWriter.WriteEnd();
                }
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(204, responseMessage.StatusCode);
            }
        }
    }
}
