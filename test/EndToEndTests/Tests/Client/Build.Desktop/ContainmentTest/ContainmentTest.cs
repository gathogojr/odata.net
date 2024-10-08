﻿//---------------------------------------------------------------------
// <copyright file="ContainmentTest.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.Test.OData.Tests.Client.ContainmentTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.OData.Client;
    using Microsoft.OData;
    using Microsoft.OData.Edm;
    using Microsoft.Test.OData.Services.TestServices;
    using Microsoft.Test.OData.Services.TestServices.ODataWCFServiceReference;
    using Microsoft.Test.OData.Tests.Client.Common;
    using HttpWebRequestMessage = Microsoft.Test.OData.Tests.Client.Common.HttpWebRequestMessage;
    using Xunit;

    /// <summary>
    /// Send query and verify the results from the service implemented using ODataLib and EDMLib.
    /// </summary>
    public class ContainmentTest : ODataWCFServiceTestsBase<InMemoryEntities>, IDisposable
    {
        private const string TestModelNameSpace = "Microsoft.Test.OData.Services.ODataWCFService";

        public ContainmentTest()
            : base(ServiceDescriptors.ODataWCFServiceDescriptor)
        {

        }

        private readonly string[] containmentMimeTypes = new string[]
            {
                MimeTypes.ApplicationJson + MimeTypes.ODataParameterFullMetadata,
                MimeTypes.ApplicationJson + MimeTypes.ODataParameterMinimalMetadata,
                MimeTypes.ApplicationJson + MimeTypes.ODataParameterNoMetadata,
            };

        #region Query
        [Fact]
        public void QueryContainedEntity()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(101)/MyGiftCard", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.Equal(301, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "GiftCardID")).Value);
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void CallFunctionBoundedToContainedEntity()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri +
                "Accounts(101)/MyGiftCard/Microsoft.Test.OData.Services.ODataWCFService.GetActualAmount(bonusRate=0.2)", UriKind.Absolute));
            requestMessage.SetHeader("Accept", "*/*");
            var responseMessage = requestMessage.GetResponse();
            Assert.Equal(200, responseMessage.StatusCode);

            using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
            {
                var amount = messageReader.ReadProperty().Value;
                Assert.Equal(23.88, amount);
            }
        }

        [Fact]
        public void CallFunctionWhichReturnsContainedEntity()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(101)/Microsoft.Test.OData.Services.ODataWCFService.GetDefaultPI()", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        List<ODataResource> entries = new List<ODataResource>();
                        var reader = messageReader.CreateODataResourceReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.NotNull(entry);
                                entries.Add(entry);
                            }
                        }
                        Assert.Equal(101901, Assert.IsType<ODataProperty>(entries.Single().Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                    }
                }
            }
        }

        [Fact]
        public void InvokeActionReturnsContainedEntity()
        {
            var writerSettings = new ODataMessageWriterSettings();
            writerSettings.BaseUri = ServiceBaseUri;
            var readerSettings = new ODataMessageReaderSettings();
            readerSettings.BaseUri = ServiceBaseUri;

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri + "Accounts(101)/Microsoft.Test.OData.Services.ODataWCFService.RefreshDefaultPI"));

                requestMessage.SetHeader("Content-Type", mimeType);
                requestMessage.SetHeader("Accept", mimeType);
                requestMessage.Method = "POST";
                DateTimeOffset newDate = new DateTimeOffset(DateTime.Now);
                using (var messageWriter = new ODataMessageWriter(requestMessage, writerSettings, Model))
                {
                    var odataWriter = messageWriter.CreateODataParameterWriter((IEdmOperation)null);
                    odataWriter.WriteStart();
                    odataWriter.WriteValue("newDate", newDate);
                    odataWriter.WriteEnd();
                }

                // send the http request
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceReader();
                        List<ODataResource> entries = new List<ODataResource>();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                entries.Add(entry);
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                        var properties = entries.Single().Properties.OfType<ODataProperty>();
                        Assert.Equal(101901, properties.Single(p => p.Name == "PaymentInstrumentID").Value);
                        Assert.Equal(newDate, properties.Single(p => p.Name == "CreatedDate").Value);
                    }
                }
            }
        }

        [Fact]
        public void QueryContainedEntitySet()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(103)/MyPaymentInstruments", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        List<ODataResource> entries = new List<ODataResource>();
                        var reader = messageReader.CreateODataResourceSetReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.NotNull(entry);
                                entries.Add(entry);
                            }
                            else if (reader.State == ODataReaderState.ResourceSetEnd)
                            {
                                Assert.NotNull(reader.Item as ODataResourceSet);
                            }
                        }

                        Assert.Equal(ODataReaderState.Completed, reader.State);
                        Assert.Equal(4, entries.Count);

                        var properties = entries[2].Properties.OfType<ODataProperty>();
                        Assert.Equal(103905, properties.Single(p => p.Name == "PaymentInstrumentID").Value);
                        Assert.Equal("103 new PI", properties.Single(p => p.Name == "FriendlyName").Value);
                    }
                }
            }
        }

        [Fact]
        public void QuerySpecificEntityInContainedEntitySet()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(103)/MyPaymentInstruments(103902)", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.Equal("103 second PI", Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "FriendlyName")).Value);
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryLevel2ContainedEntity()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(103)/MyPaymentInstruments(103901)/BillingStatements(103901001)", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.Equal("Digital goods: App", Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "TransactionDescription")).Value);
                            }
                        }

                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryLevel2ContainedEntitySet()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(103)/MyPaymentInstruments(103901)/BillingStatements", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceSetReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                var properties = entry.Properties.OfType<ODataProperty>();
                                Assert.NotNull(properties.Single(p => p.Name == "StatementID").Value);
                                Assert.NotNull(properties.Single(p => p.Name == "TransactionType").Value);
                                Assert.NotNull(properties.Single(p => p.Name == "TransactionDescription").Value);
                                Assert.NotNull(properties.Single(p => p.Name == "Amount").Value);
                            }
                            else if (reader.State == ODataReaderState.ResourceSetEnd)
                            {
                                Assert.NotNull(reader.Item as ODataResourceSet);
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryLeve2NonContainedEntity()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(103)/MyPaymentInstruments(103901)/TheStoredPI", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.Equal(802, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "StoredPIID")).Value);
                                Assert.Equal("AliPay", Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "PIType")).Value);
                            }
                        }

                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryLevel2Singleton()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(101)/MyPaymentInstruments(101901)/BackupStoredPI", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        List<ODataResource> entries = new List<ODataResource>();
                        var reader = messageReader.CreateODataResourceReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.NotNull(entry);
                                entries.Add(entry);
                            }
                        }

                        Assert.Equal(ODataReaderState.Completed, reader.State);
                        Assert.Single(entries);

                        Assert.Equal(800, Assert.IsType<ODataProperty>(entries[0].Properties.Single(p => p.Name == "StoredPIID")).Value);
                        Assert.Equal("The Default Stored PI", Assert.IsType<ODataProperty>(entries[0].Properties.Single(p => p.Name == "PIName")).Value);
                    }
                }
            }
        }

        [Fact]
        public void QueryContainedEntityWithDerivedTypeCast()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };
            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + string.Format("Accounts(101)/MyPaymentInstruments(101902)/{0}.CreditCardPI", TestModelNameSpace), UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.Equal(101902, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryContainedEntitySetWithDerivedTypeCast()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };
            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + string.Format("Accounts(101)/MyPaymentInstruments/{0}.CreditCardPI", TestModelNameSpace), UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceSetReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.NotNull(Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                            }
                            else if (reader.State == ODataReaderState.ResourceSetEnd)
                            {
                                Assert.NotNull(reader.Item as ODataResourceSet);
                            }
                        }

                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryContainedEntitiesInDerivedTypeEntity()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };
            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + string.Format("Accounts(101)/MyPaymentInstruments(101902)/{0}.CreditCardPI/CreditRecords", TestModelNameSpace), UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceSetReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                Assert.NotNull(Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "CreditRecordID")).Value);
                            }
                            else if (reader.State == ODataReaderState.ResourceSetEnd)
                            {
                                Assert.NotNull(reader.Item as ODataResourceSet);
                            }
                        }

                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryEntityContainsContainmentSet()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };
            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(101)", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceReader();
                        ODataResource entry = null;
                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                entry = reader.Item as ODataResource;
                            }
                        }
                        Assert.Equal(101, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "AccountID")).Value);
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryFeedContainsContainmentSet()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };
            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceSetReader();
                        ODataResourceSet feed = null;
                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                if (entry != null && entry.TypeName.EndsWith("Account"))
                                {
                                    Assert.NotNull(Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "AccountID")).Value);
                                }
                            }
                            else if (reader.State == ODataReaderState.ResourceSetEnd)
                            {
                                feed = reader.Item as ODataResourceSet;
                            }
                        }
                        Assert.NotNull(feed);
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryIndividualPropertyOfContainedEntity()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(101)/MyPaymentInstruments(101902)/PaymentInstrumentID", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        messageReader.ReadProperty();
                    }
                }
            }
        }

        [Fact]
        public void QueryContainedEntitiesWithFilter()
        {
            Dictionary<string, bool> testCases = new Dictionary<string, bool>()
            {
                { "Accounts(103)/MyPaymentInstruments?$filter=PaymentInstrumentID gt 103901", false },
            };

            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var testCase in testCases)
            {
                foreach (var mimeType in containmentMimeTypes)
                {
                    var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + testCase.Key, UriKind.Absolute));
                    requestMessage.SetHeader("Accept", mimeType);
                    var responseMessage = requestMessage.GetResponse();
                    Assert.Equal(200, responseMessage.StatusCode);

                    if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                    {
                        using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                        {
                            List<ODataResource> entries = new List<ODataResource>();
                            var reader = messageReader.CreateODataResourceSetReader();

                            while (reader.Read())
                            {
                                if (reader.State == ODataReaderState.ResourceEnd)
                                {
                                    ODataResource entry = reader.Item as ODataResource;
                                    Assert.NotNull(entry);
                                    entries.Add(entry);
                                }
                                else if (reader.State == ODataReaderState.ResourceSetEnd)
                                {
                                    Assert.NotNull(reader.Item as ODataResourceSet);
                                }
                            }

                            Assert.Equal(ODataReaderState.Completed, reader.State);
                            Assert.Equal(2, entries.Count);
                            Assert.Equal(103902, Assert.IsType<ODataProperty>(entries[0].Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                            Assert.Equal(103905, Assert.IsType<ODataProperty>(entries[1].Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                        }
                    }
                }
            }
        }

        [Fact]
        public void QueryContainedEntitiesWithOrderby()
        {
            Dictionary<string, bool> testCases = new Dictionary<string, bool>()
            {
                { "Accounts(103)/MyPaymentInstruments?$orderby=CreatedDate", false },
            };

            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var testCase in testCases)
            {
                foreach (var mimeType in containmentMimeTypes)
                {
                    var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + testCase.Key, UriKind.Absolute));
                    requestMessage.SetHeader("Accept", mimeType);
                    var responseMessage = requestMessage.GetResponse();
                    Assert.Equal(200, responseMessage.StatusCode);

                    if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                    {
                        using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                        {
                            List<ODataResource> entries = new List<ODataResource>();
                            var reader = messageReader.CreateODataResourceSetReader();

                            while (reader.Read())
                            {
                                if (reader.State == ODataReaderState.ResourceEnd)
                                {
                                    ODataResource entry = reader.Item as ODataResource;
                                    Assert.NotNull(entry);
                                    entries.Add(entry);
                                }
                                else if (reader.State == ODataReaderState.ResourceSetEnd)
                                {
                                    Assert.NotNull(reader.Item as ODataResourceSet);
                                }
                            }

                            Assert.Equal(ODataReaderState.Completed, reader.State);
                            Assert.Equal(4, entries.Count);
                            Assert.Equal(103902, Assert.IsType<ODataProperty>(entries[0].Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                            Assert.Equal(101910, Assert.IsType<ODataProperty>(entries[1].Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                            Assert.Equal(103901, Assert.IsType<ODataProperty>(entries[2].Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                            Assert.Equal(103905, Assert.IsType<ODataProperty>(entries[3].Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                        }
                    }
                }
            }
        }

        [Fact]
        public void QueryEntityAndExpandContainedEntity()
        {
            Dictionary<string, int[]> testCases = new Dictionary<string, int[]>()
            {
                { "Accounts(101)?$select=AccountInfo/FirstName, AccountInfo/LastName", new int[] {1, 0} },
                { "Accounts(101)?$select=AccountID&$expand=MyPaymentInstruments($select=PaymentInstrumentID;$expand=TheStoredPI)", new int[]{7,4} },
                { "Accounts(101)?$expand=MyPaymentInstruments($select=PaymentInstrumentID,FriendlyName)", new int[]{4, 4} },

                { "Accounts(101)?$expand=MyGiftCard($select=GiftCardID)", new int[] {2, 4} },

                { "Accounts(101)?$expand=MyGiftCard", new int[] {2, 4} },
                { "Accounts(101)?$expand=MyPaymentInstruments", new int[] {4, 15} },
                { "Accounts(101)?$select=AccountID&$expand=MyGiftCard($select=GiftCardID)", new int[] {2, 1} },
                { "Accounts(101)?$select=AccountID,MyGiftCard&$expand=MyGiftCard($select=GiftCardID)", new int[] {2, 1}  },
                {"Accounts(101)?$select=AccountID&$expand=MyPaymentInstruments($select=PaymentInstrumentID;$expand=TheStoredPI($select=StoredPIID))", new int[] {7, 4}},
            };

            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var testCase in testCases)
            {
                foreach (var mimeType in containmentMimeTypes)
                {
                    var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + testCase.Key, UriKind.Absolute));
                    requestMessage.SetHeader("Accept", mimeType);
                    var responseMessage = requestMessage.GetResponse();
                    Assert.Equal(200, responseMessage.StatusCode);

                    if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                    {
                        using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                        {
                            List<ODataResource> entries = new List<ODataResource>();
                            List<ODataNestedResourceInfo> navigationLinks = new List<ODataNestedResourceInfo>();
                            var reader = messageReader.CreateODataResourceReader();
                            Stack<ODataResource> resourceStacks = new Stack<ODataResource>();
                            Stack<bool> isNavigations = new Stack<bool>();
                            while (reader.Read())
                            {
                                switch(reader.State)
                                {
                                    case ODataReaderState.ResourceStart:
                                        {
                                            var resource = reader.Item as ODataResource;
                                            if (resource != null)
                                            {
                                                resourceStacks.Push(resource);
                                            }
                                            break;
                                        }
                                    case ODataReaderState.ResourceEnd:
                                        {
                                            var resource = reader.Item as ODataResource;
                                            if (resource != null)
                                            {
                                                resourceStacks.Pop();

                                                if (resourceStacks.Count == 0 || isNavigations.Peek())
                                                {
                                                    entries.Add(resource);
                                                }
                                            }
                                            break;
                                        }
                                    case ODataReaderState.NestedResourceInfoStart:
                                        {
                                            var nestedResourceInfo = reader.Item as ODataNestedResourceInfo;
                                            var parentResource = resourceStacks.Peek();
                                            var parentType = Model.FindDeclaredType(parentResource.TypeName);
                                            if (parentType is IEdmEntityType)
                                            {
                                                if (((IEdmEntityType)parentType).NavigationProperties().Any(n => n.Name == nestedResourceInfo.Name))
                                                {
                                                    isNavigations.Push(true);
                                                    break;
                                                }
                                            }
                                            isNavigations.Push(false);
                                            break;
                                        }
                                    case ODataReaderState.NestedResourceInfoEnd:
                                        {
                                            var nestedResourceInfo = reader.Item as ODataNestedResourceInfo;
                                            var isNavigation = isNavigations.Pop();
                                            if (isNavigation)
                                            {
                                                navigationLinks.Add(nestedResourceInfo);
                                            }
                                            break;
                                        }
                                    default:
                                        break;
                                }
                            }

                            Assert.Equal(ODataReaderState.Completed, reader.State);
                            Assert.Equal(testCase.Value[0], entries.Count);

                            Assert.Equal(testCase.Value[1], navigationLinks.Count);
                        }
                    }
                }
            }
        }

        [Fact]
        public void QueryContainedEntityWithSelectOption()
        {
            Dictionary<string, int> testCases = new Dictionary<string, int>()
            {
                { "Accounts(101)/MyGiftCard?$select=GiftCardID,GiftCardNO", 2 },
                { "Accounts(101)/MyPaymentInstruments(101901)?$select=PaymentInstrumentID,FriendlyName,CreatedDate", 3 },
            };

            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var testCase in testCases)
            {
                foreach (var mimeType in containmentMimeTypes)
                {
                    var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + testCase.Key, UriKind.Absolute));
                    requestMessage.SetHeader("Accept", mimeType);
                    var responseMessage = requestMessage.GetResponse();
                    Assert.Equal(200, responseMessage.StatusCode);

                    if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                    {
                        using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                        {
                            List<ODataResource> entries = new List<ODataResource>();
                            List<ODataNestedResourceInfo> navigationLinks = new List<ODataNestedResourceInfo>();

                            var reader = messageReader.CreateODataResourceReader();
                            while (reader.Read())
                            {
                                if (reader.State == ODataReaderState.ResourceEnd)
                                {
                                    entries.Add(reader.Item as ODataResource);
                                }
                                else if (reader.State == ODataReaderState.NestedResourceInfoEnd)
                                {
                                    navigationLinks.Add(reader.Item as ODataNestedResourceInfo);
                                }
                            }

                            Assert.Equal(ODataReaderState.Completed, reader.State);
                            Assert.Single(entries);
                            Assert.Empty(navigationLinks);
                            Assert.Equal(testCase.Value, entries[0].Properties.Count());
                        }
                    }
                }
            }
        }

        #endregion

        #region Create/Update/Delete
        [Fact]
        public void CreateAndDeleteContainmentEntity()
        {
            // create entry and insert
            var paymentInstrumentEntry = new ODataResource() { TypeName = TestModelNameSpace + ".PaymentInstrument" };
            var paymentInstrumentEntryP1 = new ODataProperty { Name = "PaymentInstrumentID", Value = 101904 };
            var paymentInstrumentEntryP2 = new ODataProperty { Name = "FriendlyName", Value = "101 new PI" };
            var paymentInstrumentEntryP3 = new ODataProperty { Name = "CreatedDate", Value = new DateTimeOffset(new DateTime(2013, 8, 29, 14, 11, 57)) };
            paymentInstrumentEntry.Properties = new[] { paymentInstrumentEntryP1, paymentInstrumentEntryP2, paymentInstrumentEntryP3 };

            var settings = new ODataMessageWriterSettings();
            settings.BaseUri = ServiceBaseUri;

            var accountType = Model.FindDeclaredType(TestModelNameSpace + ".Account") as IEdmEntityType;
            var accountSet = Model.EntityContainer.FindEntitySet("Accounts");
            var paymentInstrumentType = Model.FindDeclaredType(TestModelNameSpace + ".PaymentInstrument") as IEdmEntityType;
            IEdmNavigationProperty navProp = accountType.FindProperty("MyPaymentInstruments") as IEdmNavigationProperty;
            var myPaymentInstrumentSet = accountSet.FindNavigationTarget(navProp);

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri + "Accounts(101)/MyPaymentInstruments"));

                requestMessage.SetHeader("Content-Type", mimeType);
                requestMessage.SetHeader("Accept", mimeType);
                requestMessage.Method = "POST";
                using (var messageWriter = new ODataMessageWriter(requestMessage, settings, Model))
                {
                    var odataWriter = messageWriter.CreateODataResourceWriter(myPaymentInstrumentSet, paymentInstrumentType);
                    odataWriter.WriteStart(paymentInstrumentEntry);
                    odataWriter.WriteEnd();
                }

                // send the http request
                var responseMessage = requestMessage.GetResponse();

                // verify the create
                Assert.Equal(201, responseMessage.StatusCode);
                ODataResource entry = this.QueryEntityItem("Accounts(101)/MyPaymentInstruments(101904)") as ODataResource;
                Assert.Equal(101904, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);

                // delete the entry
                var deleteRequestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri + "Accounts(101)/MyPaymentInstruments(101904)"));
                deleteRequestMessage.Method = "DELETE";
                var deleteResponseMessage = deleteRequestMessage.GetResponse();

                // verify the delete
                Assert.Equal(204, deleteResponseMessage.StatusCode);
                ODataResource deletedEntry = this.QueryEntityItem("Accounts(101)/MyPaymentInstruments(101904)", 204) as ODataResource;
                Assert.Null(deletedEntry);
            }
        }

        [Fact]
        public void CreateSingleValuedContainedEntity()
        {
            // create entry and insert
            var giftCardEntry = new ODataResource() { TypeName = TestModelNameSpace + ".GiftCard" };
            var giftCardEntryP1 = new ODataProperty { Name = "GiftCardID", Value = 304 };
            var giftCardEntryP2 = new ODataProperty { Name = "GiftCardNO", Value = "AAGS993A" };
            var giftCardEntryP3 = new ODataProperty { Name = "ExperationDate", Value = new DateTimeOffset(new DateTime(2013, 12, 30)) };
            var giftCardEntryP4 = new ODataProperty { Name = "Amount", Value = 37.0 };
            giftCardEntry.Properties = new[] { giftCardEntryP1, giftCardEntryP2, giftCardEntryP3, giftCardEntryP4 };

            var settings = new ODataMessageWriterSettings();
            settings.BaseUri = ServiceBaseUri;

            var accountType = Model.FindDeclaredType(TestModelNameSpace + ".Account") as IEdmEntityType;
            var accountSet = Model.EntityContainer.FindEntitySet("Accounts");
            var giftCardType = Model.FindDeclaredType(TestModelNameSpace + ".GiftCard") as IEdmEntityType;
            IEdmNavigationProperty navProp = accountType.FindProperty("MyGiftCard") as IEdmNavigationProperty;
            var myGiftCardSet = accountSet.FindNavigationTarget(navProp);

            foreach (var mimeType in containmentMimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri + "Accounts(104)/MyGiftCard"));

                requestMessage.SetHeader("Content-Type", mimeType);
                requestMessage.SetHeader("Accept", mimeType);
                // Use PATCH to upsert
                requestMessage.Method = "PATCH";
                using (var messageWriter = new ODataMessageWriter(requestMessage, settings, Model))
                {
                    var odataWriter = messageWriter.CreateODataResourceWriter(myGiftCardSet, giftCardType);
                    odataWriter.WriteStart(giftCardEntry);
                    odataWriter.WriteEnd();
                }

                // send the http request
                var responseMessage = requestMessage.GetResponse();

                // verify the create
                // TODO: [tiano] the response code should be 201
                Assert.Equal(204, responseMessage.StatusCode);
                ODataResource entry = this.QueryEntityItem("Accounts(104)/MyGiftCard") as ODataResource;
                Assert.Equal(304, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "GiftCardID")).Value);
            }
        }

        [Fact]
        public void UpdateContainmentEntity()
        {
            // create entry and insert
            var settings = new ODataMessageWriterSettings();
            settings.BaseUri = ServiceBaseUri;

            var accountType = Model.FindDeclaredType(TestModelNameSpace + ".Account") as IEdmEntityType;
            var accountSet = Model.EntityContainer.FindEntitySet("Accounts");
            var paymentInstrumentType = Model.FindDeclaredType(TestModelNameSpace + ".PaymentInstrument") as IEdmEntityType;
            IEdmNavigationProperty navProp = accountType.FindProperty("MyPaymentInstruments") as IEdmNavigationProperty;
            var myPaymentInstrumentSet = accountSet.FindNavigationTarget(navProp);

            foreach (var mimeType in containmentMimeTypes)
            {
                var paymentInstrumentEntry = new ODataResource() { TypeName = TestModelNameSpace + ".PaymentInstrument" };
                var paymentInstrumentEntryP1 = new ODataProperty { Name = "PaymentInstrumentID", Value = 101903 };
                var paymentInstrumentEntryP2 = new ODataProperty { Name = "FriendlyName", Value = mimeType };
                var paymentInstrumentEntryP3 = new ODataProperty { Name = "CreatedDate", Value = new DateTimeOffset(new DateTime(2013, 8, 29, 14, 11, 57)) };
                paymentInstrumentEntry.Properties = new[] { paymentInstrumentEntryP1, paymentInstrumentEntryP2, paymentInstrumentEntryP3 };

                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri + "Accounts(101)/MyPaymentInstruments(101903)"));

                requestMessage.SetHeader("Content-Type", mimeType);
                requestMessage.SetHeader("Accept", mimeType);
                requestMessage.Method = "PATCH";
                using (var messageWriter = new ODataMessageWriter(requestMessage, settings, Model))
                {
                    var odataWriter = messageWriter.CreateODataResourceWriter(myPaymentInstrumentSet, paymentInstrumentType);
                    odataWriter.WriteStart(paymentInstrumentEntry);
                    odataWriter.WriteEnd();
                }

                // send the http request
                var responseMessage = requestMessage.GetResponse();

                // verify the create
                Assert.Equal(204, responseMessage.StatusCode);
                ODataResource entry = this.QueryEntityItem("Accounts(101)/MyPaymentInstruments(101903)") as ODataResource;
                Assert.Equal(101903, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                Assert.Equal(mimeType, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "FriendlyName")).Value);
            }
        }


        [Fact]
        public void UpsertContainmentEntity()
        {
            // create entry and insert
            var settings = new ODataMessageWriterSettings();
            settings.BaseUri = ServiceBaseUri;

            var accountType = Model.FindDeclaredType(TestModelNameSpace + ".Account") as IEdmEntityType;
            var accountSet = Model.EntityContainer.FindEntitySet("Accounts");
            var paymentInstrumentType = Model.FindDeclaredType(TestModelNameSpace + ".PaymentInstrument") as IEdmEntityType;
            IEdmNavigationProperty navProp = accountType.FindProperty("MyPaymentInstruments") as IEdmNavigationProperty;
            var myPaymentInstrumentSet = accountSet.FindNavigationTarget(navProp);

            int count = 1;

            foreach (var mimeType in containmentMimeTypes)
            {
                int piid = 20000 + count;
                var paymentInstrumentEntry = new ODataResource() { TypeName = TestModelNameSpace + ".PaymentInstrument" };
                var paymentInstrumentEntryP1 = new ODataProperty { Name = "PaymentInstrumentID", Value = piid };
                var paymentInstrumentEntryP2 = new ODataProperty { Name = "FriendlyName", Value = mimeType };
                var paymentInstrumentEntryP3 = new ODataProperty { Name = "CreatedDate", Value = new DateTimeOffset(new DateTime(2013, 8, 29, 14, 11, 57)) };
                paymentInstrumentEntry.Properties = new[] { paymentInstrumentEntryP1, paymentInstrumentEntryP2, paymentInstrumentEntryP3 };

                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri + "Accounts(101)/MyPaymentInstruments(" + piid + ")"));

                requestMessage.SetHeader("Content-Type", mimeType);
                requestMessage.SetHeader("Accept", mimeType);
                requestMessage.Method = "PUT";
                using (var messageWriter = new ODataMessageWriter(requestMessage, settings, Model))
                {
                    var odataWriter = messageWriter.CreateODataResourceWriter(myPaymentInstrumentSet, paymentInstrumentType);
                    odataWriter.WriteStart(paymentInstrumentEntry);
                    odataWriter.WriteEnd();
                }

                // send the http request
                var responseMessage = requestMessage.GetResponse();

                // verify the create
                Assert.Equal(201, responseMessage.StatusCode);
                ODataResource entry = this.QueryEntityItem("Accounts(101)/MyPaymentInstruments(" + piid + ")") as ODataResource;
                Assert.Equal(piid, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "PaymentInstrumentID")).Value);
                Assert.Equal(mimeType, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "FriendlyName")).Value);

                count++;
            }
        }

        #endregion

        #region OData Client test cases

        [Fact]
        public void QueryContainedEntityFromODataClient()
        {
            TestClientContext.Format.UseJson(Model);

            var queryable = TestClientContext.CreateQuery<GiftCard>("Accounts(101)/MyGiftCard");
            Assert.EndsWith("Accounts(101)/MyGiftCard", queryable.RequestUri.OriginalString, StringComparison.Ordinal);

            List<GiftCard> result = queryable.ToList();
            Assert.Single(result);
            Assert.Equal(301, result[0].GiftCardID);
            Assert.Equal("AAA123A", result[0].GiftCardNO);
        }

        [Fact]
        public void QueryContainedEntitySetFromODataClient()
        {
            TestClientContext.Format.UseJson(Model);

            var queryable = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments");
            Assert.EndsWith("Accounts(103)/MyPaymentInstruments", queryable.RequestUri.OriginalString, StringComparison.Ordinal);

            List<PaymentInstrument> result = queryable.ToList();
            Assert.Equal(4, result.Count);
            Assert.Equal(103902, result[1].PaymentInstrumentID);
            Assert.Equal("103 second PI", result[1].FriendlyName);
        }

        [Fact]
        public void QuerySpecificEntityInContainedEntitySetFromODataClient()
        {
            TestClientContext.Format.UseJson(Model);

            var queryable = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments(103902)");
            Assert.EndsWith("Accounts(103)/MyPaymentInstruments(103902)", queryable.RequestUri.OriginalString, StringComparison.Ordinal);

            List<PaymentInstrument> result = queryable.ToList();
            Assert.Single(result);
            Assert.Equal(103902, result[0].PaymentInstrumentID);
            Assert.Equal("103 second PI", result[0].FriendlyName);

        }

        [Fact]
        public void QueryIndividualPropertyOfContainedEntityFromODataClient()
        {
            TestClientContext.Format.UseJson(Model);
            var queryable = TestClientContext.CreateQuery<int>("Accounts(103)/MyPaymentInstruments(103902)/PaymentInstrumentID");
            Assert.EndsWith("Accounts(103)/MyPaymentInstruments(103902)/PaymentInstrumentID", queryable.RequestUri.OriginalString, StringComparison.Ordinal);

            List<int> result = queryable.ToList();
            Assert.Single(result);
            Assert.Equal(103902, result[0]);

        }

        [Fact]
        public void LinqUriTranslationTest()
        {
            TestClientContext.Format.UseJson(Model);
            TestClientContext.MergeOption = MergeOption.OverwriteChanges;

            //translate to key
            var q1 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").Where(pi => pi.PaymentInstrumentID == 103901);
            PaymentInstrument q1Result = q1.Single();
            Assert.Equal(103901, q1Result.PaymentInstrumentID);

            //$filter
            var q2 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").Where(pi => pi.CreatedDate > new DateTimeOffset(new DateTime(2013, 10, 1)));
            PaymentInstrument q2Result = q2.Single();
            Assert.Equal(103905, q2Result.PaymentInstrumentID);

            //$orderby
            var q3 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").OrderBy(pi => pi.CreatedDate).ThenByDescending(pi => pi.FriendlyName);
            List<PaymentInstrument> q3Result = q3.ToList();
            Assert.Equal(103902, q3Result[0].PaymentInstrumentID);

            //$expand
            var q4 = TestClientContext.Accounts.Expand(account => account.MyPaymentInstruments).Where(account => account.AccountID == 103);
            Account q4Result = q4.Single();
            Assert.NotNull(q4Result.MyPaymentInstruments);

            var q5 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").Expand(pi => pi.BillingStatements).Where(pi => pi.PaymentInstrumentID == 103901);
            PaymentInstrument q5Result = q5.Single();
            Assert.NotNull(q5Result.BillingStatements);

            //$top
            var q6 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").Take(1);
            var q6Result = q6.ToList();

            //$count
            var q7 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").Count();

            //$count=true
            var q8 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").IncludeCount();
            var q8Result = q8.ToList();

            //projection
            var q9 = TestClientContext.Accounts.Where(a => a.AccountID == 103).Select(a => new Account() { AccountID = a.AccountID, MyGiftCard = a.MyGiftCard });
            var q9Result = q9.Single();
            Assert.NotNull(q9Result.MyGiftCard);

            var q10 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(103)/MyPaymentInstruments").Where(pi => pi.PaymentInstrumentID == 103901).Select(p => new PaymentInstrument()
            {
                PaymentInstrumentID = p.PaymentInstrumentID,
                BillingStatements = p.BillingStatements
            });
            var q10Result = q10.ToList();
        }

        [Fact]
        public void CallFunctionBoundToContainedEntityFromODataClient()
        {
            double result = TestClientContext.Execute<double>(new Uri(ServiceBaseUri.AbsoluteUri +
                "Accounts(101)/MyGiftCard/Microsoft.Test.OData.Services.ODataWCFService.GetActualAmount(bonusRate=0.2)", UriKind.Absolute), "GET", true).Single();

            Assert.Equal(23.88, result);

        }

        [Fact]
        public void CallFunctionFromODataClientWhichReturnsContainedEntity()
        {
            TestClientContext.Format.UseJson(Model);

            PaymentInstrument result = TestClientContext.Execute<PaymentInstrument>(new Uri(ServiceBaseUri.AbsoluteUri +
                "Accounts(101)/Microsoft.Test.OData.Services.ODataWCFService.GetDefaultPI()", UriKind.Absolute), "GET", true).Single();
            Assert.Equal(101901, result.PaymentInstrumentID);

            result.FriendlyName = "Random Name";
            TestClientContext.UpdateObject(result);
            TestClientContext.SaveChanges();

            result = TestClientContext.Execute<PaymentInstrument>(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(101)/MyPaymentInstruments(101901)", UriKind.Absolute), "GET", true).Single();
            Assert.Equal("Random Name", result.FriendlyName);
        }

        [Fact]
        public void InvokeActionFromODataClientWhichReturnsContainedEntity()
        {
            TestClientContext.Format.UseJson(Model);

            PaymentInstrument result = TestClientContext.Execute<PaymentInstrument>(new Uri(ServiceBaseUri.AbsoluteUri +
                "Accounts(101)/Microsoft.Test.OData.Services.ODataWCFService.RefreshDefaultPI", UriKind.Absolute), "POST", true,
                new BodyOperationParameter("newDate", new DateTimeOffset(DateTime.Now))).Single();
            Assert.Equal(101901, result.PaymentInstrumentID);

            result.FriendlyName = "Random Name";
            TestClientContext.UpdateObject(result);
            TestClientContext.SaveChanges();

            result = TestClientContext.Execute<PaymentInstrument>(new Uri(ServiceBaseUri.AbsoluteUri + "Accounts(101)/MyPaymentInstruments(101901)", UriKind.Absolute), "GET").Single();
            Assert.Equal("Random Name", result.FriendlyName);
        }

        [Fact]
        public void CreateContainedEntityFromODataClientUsingAddRelatedObject()
        {

                    TestClientContext.Format.UseJson(Model);

                // create an an account entity and a contained PI entity
                Account newAccount = new Account()
                {
                    AccountID = 110,
                    CountryRegion = "CN",
                    AccountInfo = new AccountInfo()
                    {
                        FirstName = "New",
                        LastName = "Guy"
                    }
                };
                PaymentInstrument newPI = new PaymentInstrument()
                {
                    PaymentInstrumentID = 110901,
                    FriendlyName = "110's first PI",
                    CreatedDate = new DateTimeOffset(new DateTime(2012, 12, 10))
                };
                TestClientContext.AddToAccounts(newAccount);
                TestClientContext.AddRelatedObject(newAccount, "MyPaymentInstruments", newPI);
                TestClientContext.SaveChanges();

                var queryable0 = TestClientContext.Accounts.Where(account => account.AccountID == 110);
                Account accountResult = queryable0.Single();
                Assert.Equal("Guy", accountResult.AccountInfo.LastName);

                var queryable1 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(110)/MyPaymentInstruments").Where(pi => pi.PaymentInstrumentID == 110901);
                PaymentInstrument piResult = queryable1.Single();
                Assert.Equal("110's first PI", piResult.FriendlyName);
        }

        [Fact]
        public void DeleteContainedEntityFromODataClientUsingDeleteObject()
        {
            TestClientContext.Format.UseJson(Model);

            // create an an account entity and a contained PI entity
            Account newAccount = new Account()
            {
                AccountID = 115,
                CountryRegion = "CN",
                AccountInfo = new AccountInfo()
                {
                    FirstName = "New",
                    LastName = "Guy"
                }
            };
            PaymentInstrument newPI = new PaymentInstrument()
            {
                PaymentInstrumentID = 115901,
                FriendlyName = "115's first PI",
                CreatedDate = new DateTimeOffset(new DateTime(2012, 12, 10))
            };
            TestClientContext.AddToAccounts(newAccount);
            TestClientContext.AddRelatedObject(newAccount, "MyPaymentInstruments", newPI);
            TestClientContext.SaveChanges();

            var queryable = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(115)/MyPaymentInstruments");
            PaymentInstrument piResult = queryable.Single();
            Assert.Equal("115's first PI", piResult.FriendlyName);

            TestClientContext.DeleteObject(piResult);
            TestClientContext.SaveChanges();

            List<PaymentInstrument> piResult2 = queryable.ToList();
            Assert.Empty(piResult2);
        }

        [Fact]
        public void UpdateContainedEntityFromODataClientUsingUpdateObject()
        {
            TestClientContext.Format.UseJson(Model);

            // Get a contained PI entity
            var queryable1 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(101)/MyPaymentInstruments").Where(pi => pi.PaymentInstrumentID == 101901);
            PaymentInstrument piResult = queryable1.Single();

            piResult.FriendlyName = "Michael's first PI";
            TestClientContext.UpdateObject(piResult);
            TestClientContext.SaveChanges();

            piResult = queryable1.Single();
            Assert.Equal("Michael's first PI", piResult.FriendlyName);
        }

        // [Fact] // github issuse: #896
        internal void CreateContainedEntityFromODataClientUsingAddRelatedObjectUsingBatchRequest()
        {
            TestClientContext.Format.UseJson(Model);

            // create an an account entity and a contained PI entity
            Account newAccount = new Account()
            {
                AccountID = 114,
                CountryRegion = "CN",
                AccountInfo = new AccountInfo()
                {
                    FirstName = "New",
                    LastName = "Guy"
                }
            };
            PaymentInstrument newPI = new PaymentInstrument()
            {
                PaymentInstrumentID = 110905,
                FriendlyName = "110's first PI",
                CreatedDate = new DateTimeOffset(new DateTime(2012, 12, 10))
            };
            TestClientContext.AddToAccounts(newAccount);
            TestClientContext.AddRelatedObject(newAccount, "MyPaymentInstruments", newPI);
            TestClientContext.SaveChanges(SaveChangesOptions.BatchWithIndependentOperations);

            var queryable0 = TestClientContext.CreateQuery<Account>("Accounts");
            List<Account> accountResult = queryable0.ToList();

            var queryable1 = TestClientContext.CreateQuery<PaymentInstrument>("Accounts(114)/MyPaymentInstruments");
            List<PaymentInstrument> piResult = queryable1.ToList();

        }

        [Fact]
        public void CreateContainedNonCollectionEntityFromODataClientUsingUpdateRelatedObject()
        {
            TestClientContext.Format.UseJson(Model);

            // create an an account entity and a contained PI entity
            Account newAccount = new Account()
            {
                AccountID = 120,
                CountryRegion = "GB",
                AccountInfo = new AccountInfo()
                {
                    FirstName = "Diana",
                    LastName = "Spencer"
                }
            };
            GiftCard giftCard = new GiftCard()
            {
                GiftCardID = 320,
                GiftCardNO = "XX120ABCDE",
                Amount = 76,
                ExperationDate = new DateTimeOffset(new DateTime(2013, 12, 30))
            };

            TestClientContext.AddToAccounts(newAccount);
            TestClientContext.UpdateRelatedObject(newAccount, "MyGiftCard", giftCard);
            TestClientContext.SaveChanges();

            var queryable1 = TestClientContext.CreateQuery<GiftCard>("Accounts(120)/MyGiftCard");
            List<GiftCard> giftCardResult = queryable1.ToList();
            Assert.Single(giftCardResult);
            Assert.Equal(76, giftCardResult[0].Amount);
        }

        #endregion

        #region private methods

        private ODataItem QueryEntityItem(string uri, int expectedStatusCode = 200)
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            var queryRequestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + uri, UriKind.Absolute));
            queryRequestMessage.SetHeader("Accept", MimeTypes.ApplicationJson);
            var queryResponseMessage = queryRequestMessage.GetResponse();
            Assert.Equal(expectedStatusCode, queryResponseMessage.StatusCode);

            ODataItem item = null;
            if (expectedStatusCode == 200)
            {
                using (var messageReader = new ODataMessageReader(queryResponseMessage, readerSettings, Model))
                {
                    var reader = messageReader.CreateODataResourceReader();
                    while (reader.Read())
                    {
                        if (reader.State == ODataReaderState.ResourceEnd)
                        {
                            item = reader.Item;
                        }
                    }

                    Assert.Equal(ODataReaderState.Completed, reader.State);
                }
            }

            return item;
        }
        #endregion

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
