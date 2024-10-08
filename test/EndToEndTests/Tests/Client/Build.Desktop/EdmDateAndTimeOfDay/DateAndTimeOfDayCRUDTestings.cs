﻿//---------------------------------------------------------------------
// <copyright file="DateAndTimeOfDayCRUDTestings.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.Test.OData.Tests.Client.EdmDateAndTimeOfDay
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.OData;
    using Microsoft.OData.Edm;
    using Microsoft.Test.OData.Services.TestServices;
    using Microsoft.Test.OData.Services.TestServices.ODataWCFServiceReference;
    using Microsoft.Test.OData.Tests.Client.Common;
    using Xunit;

    public class DateAndTimeOfDayCRUDTestings : ODataWCFServiceTestsBase<InMemoryEntities>, IDisposable
    {
        public DateAndTimeOfDayCRUDTestings()
            : base(ServiceDescriptors.ODataWCFServiceDescriptor)
        {

        }

        #region Query/Action/Function
        [Fact]
        public void QueryEntityContainsDateAndTimeOfDay()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)", UriKind.Absolute));
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

                        // Verify Date Property
                        Assert.Equal(new Date(2014, 8, 31), Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipDate")).Value);
                        Assert.Equal(new TimeOfDay(12, 40, 5, 50), Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipTime")).Value);
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryTopLevelProperies()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/ShipDate", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        ODataProperty property = messageReader.ReadProperty();
                        Assert.Equal(new Date(2014, 8, 31), property.Value);
                    }
                }
            }

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/ShipTime", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        ODataProperty property = messageReader.ReadProperty();
                        Assert.Equal(new TimeOfDay(12, 40, 5, 50), property.Value);
                    }
                }
            }
        }

        [Fact]
        public void QueryRawValue()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/ShipDate/$value", UriKind.Absolute));
            var responseMessage = requestMessage.GetResponse();
            Assert.Equal(200, responseMessage.StatusCode);
            using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
            {
                var date = messageReader.ReadValue(EdmCoreModel.Instance.GetDate(false));
                Assert.Equal(new Date(2014, 8, 31), date);
            }

            var requestMessage2 = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/ShipTime/$value", UriKind.Absolute));
            var responseMessage2 = requestMessage2.GetResponse();
            Assert.Equal(200, responseMessage2.StatusCode);
            using (var messageReader = new ODataMessageReader(responseMessage2, readerSettings, Model))
            {
                var date = messageReader.ReadValue(EdmCoreModel.Instance.GetTimeOfDay(false));
                Assert.Equal(new TimeOfDay(12, 40, 5, 50), date);
            }
        }

        [Fact]
        public void QueryWithFilterDate()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders?$filter=ShipDate eq 2014-08-31", UriKind.Absolute));
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
                                if (entry != null && entry.TypeName.EndsWith("Order"))
                                {
                                    // Verify Date Property
                                    Assert.Equal(new Date(2014, 8, 31), Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipDate")).Value);
                                    Assert.Equal(new TimeOfDay(12, 40, 5, 50), Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipTime")).Value);
                                }
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryWithFilterTime()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders?$filter=ShipTime eq 12:40:5.05", UriKind.Absolute));
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
                                if (entry != null)
                                {
                                    // Verify Date Property
                                    Assert.Equal(new Date(2014, 8, 31), Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipDate")).Value);
                                    Assert.Equal(new TimeOfDay(12, 40, 5, 50), Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipTime")).Value);
                                }
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryWithOrderByDate()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders?$OrderBy=ShipDate", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    var list = new List<ODataResource>();
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceSetReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                if (entry != null)
                                {
                                    foreach (ODataResource pre in list)
                                    {
                                        Date entryDate = Assert.IsType<Date>(Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipDate")).Value);
                                        Date preDate = Assert.IsType<Date>(Assert.IsType<ODataProperty>(pre.Properties.Single(p => p.Name == "ShipDate")).Value);
                                        Assert.True(preDate > entryDate);
                                    }

                                    list.Add(entry);
                                }
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void QueryWithOrderByTime()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders?$OrderBy=ShipTime", UriKind.Absolute));
                requestMessage.SetHeader("Accept", mimeType);
                var responseMessage = requestMessage.GetResponse();
                Assert.Equal(200, responseMessage.StatusCode);

                if (!mimeType.Contains(MimeTypes.ODataParameterNoMetadata))
                {
                    var list = new List<ODataResource>();
                    using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
                    {
                        var reader = messageReader.CreateODataResourceSetReader();

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                if (entry != null)
                                {
                                    foreach (ODataResource pre in list)
                                    {
                                        TimeOfDay entryTimeOfDay = Assert.IsType<TimeOfDay>(Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipTime")).Value);
                                        TimeOfDay preTimeOfDay = Assert.IsType<TimeOfDay>(Assert.IsType<ODataProperty>(pre.Properties.Single(p => p.Name == "ShipTime")).Value);
                                        Assert.True(preTimeOfDay > entryTimeOfDay);
                                    }

                                    list.Add(entry);
                                }
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }

        [Fact]
        public void FunctionReturnDate()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/Microsoft.Test.OData.Services.ODataWCFService.GetShipDate", UriKind.Absolute));
            requestMessage.SetHeader("Accept", "*/*");
            var responseMessage = requestMessage.GetResponse();
            Assert.Equal(200, responseMessage.StatusCode);

            using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
            {
                var date = messageReader.ReadProperty().Value;
                Assert.Equal(new Date(2014, 8, 31), date);
            }
        }

        [Fact]
        public void FunctionWithDate()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/Microsoft.Test.OData.Services.ODataWCFService.CheckShipDate(date = 2014-08-31)", UriKind.Absolute));
            requestMessage.SetHeader("Accept", "*/*");
            var responseMessage = requestMessage.GetResponse();
            Assert.Equal(200, responseMessage.StatusCode);

            using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
            {
                var True = messageReader.ReadProperty().Value;
                Assert.Equal(true, True);
            }
        }

        [Fact]
        public void FunctionReturnTime()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/Microsoft.Test.OData.Services.ODataWCFService.GetShipTime", UriKind.Absolute));
            requestMessage.SetHeader("Accept", "*/*");
            var responseMessage = requestMessage.GetResponse();
            Assert.Equal(200, responseMessage.StatusCode);

            using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
            {
                var time = messageReader.ReadProperty().Value;
                Assert.Equal(new TimeOfDay(12, 40, 5, 50), time);
            }
        }

        [Fact]
        public void FunctionWithTime()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Orders(7)/Microsoft.Test.OData.Services.ODataWCFService.CheckShipTime(time = 12:40:5.5)", UriKind.Absolute));
            requestMessage.SetHeader("Accept", "*/*");
            var responseMessage = requestMessage.GetResponse();
            Assert.Equal(200, responseMessage.StatusCode);

            using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, Model))
            {
                var True = messageReader.ReadProperty().Value;
                Assert.Equal(false, True);
            }
        }

        [Fact]
        public void QueryByDateKey()
        {
            ODataMessageReaderSettings readerSettings = new ODataMessageReaderSettings() { BaseUri = ServiceBaseUri };

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri.AbsoluteUri + "Calendars(2015-11-11)", UriKind.Absolute));
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
                                // Verify Date Property
                                Assert.Equal(new Date(2015, 11, 11), Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "Day")).Value);
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);
                    }
                }
            }
        }
        [Fact]
        public void ActionTakeDateAndTimeAsParameter()
        {
            var writerSettings = new ODataMessageWriterSettings();
            writerSettings.BaseUri = ServiceBaseUri;
            var readerSettings = new ODataMessageReaderSettings();
            readerSettings.BaseUri = ServiceBaseUri;

            foreach (var mimeType in mimeTypes)
            {
                var requestMessage = new HttpWebRequestMessage(new Uri(ServiceBaseUri + "Orders(7)/Microsoft.Test.OData.Services.ODataWCFService.ChangeShipTimeAndDate"));

                requestMessage.SetHeader("Content-Type", mimeType);
                requestMessage.SetHeader("Accept", mimeType);
                requestMessage.Method = "POST";

                Date newDate = Date.MinValue;
                TimeOfDay newTime = TimeOfDay.MinValue;
                using (var messageWriter = new ODataMessageWriter(requestMessage, writerSettings, Model))
                {
                    var odataWriter = messageWriter.CreateODataParameterWriter((IEdmOperation)null);
                    odataWriter.WriteStart();
                    odataWriter.WriteValue("date", newDate);
                    odataWriter.WriteValue("time", newTime);
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

                        while (reader.Read())
                        {
                            if (reader.State == ODataReaderState.ResourceEnd)
                            {
                                ODataResource entry = reader.Item as ODataResource;
                                if (entry != null)
                                {
                                    Assert.Equal(Date.MinValue, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipDate")).Value);
                                    Assert.Equal(TimeOfDay.MinValue, Assert.IsType<ODataProperty>(entry.Properties.Single(p => p.Name == "ShipTime")).Value);
                                }
                            }
                        }
                        Assert.Equal(ODataReaderState.Completed, reader.State);

                    }
                }
            }
        }

        #endregion

#region Client

#if !(NETCOREAPP1_0 || NETCOREAPP2_0)
        [Fact]
        public void ClientTest()
        {
            // Query Entity Contain Date/TimeOfDay
            TestClientContext.MergeOption = Microsoft.OData.Client.MergeOption.OverwriteChanges;

            // Query Property
            var shipDate = TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).Select(o => o.ShipDate).GetValue();
            Assert.Equal(new Date(2014, 8, 31), shipDate);

            var shipTime = TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).Select(o => o.ShipTime).GetValue();
            Assert.Equal(new TimeOfDay(12, 40, 05, 50), shipTime);

            // Projection Select
            var projOrder = TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).Select(o => new Order() { ShipDate = o.ShipDate, ShipTime = o.ShipTime }).GetValue();
            Assert.True(projOrder != null);
            Assert.Equal(new Date(2014, 8, 31), projOrder.ShipDate);
            Assert.Equal(new TimeOfDay(12, 40, 05, 50), projOrder.ShipTime);

            // Update Properties
            var order = TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).GetValue();
            Assert.True(order != null);
            Assert.Equal(new Date(2014, 8, 31), order.ShipDate);
            Assert.Equal(new TimeOfDay(12, 40, 05, 50), order.ShipTime);

            order.ShipDate = new Date(2014, 9, 30);
            TestClientContext.UpdateObject(order);
            TestClientContext.SaveChanges();

            var updatedOrder = TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).GetValue();
            Assert.Equal(new Date(2014, 9, 30), updatedOrder.ShipDate);

            // Function
            var date = TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).GetShipDate().GetValue();
            Assert.Equal(new Date(2014, 9, 30), date);

            // Action
            TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).ChangeShipTimeAndDate(Date.MaxValue, TimeOfDay.MaxValue).GetValue();
            updatedOrder = TestClientContext.Orders.ByKey(new Dictionary<string, object>() { { "OrderID", 7 } }).GetValue();
            Assert.Equal(Date.MaxValue, updatedOrder.ShipDate);
            Assert.Equal(TimeOfDay.MaxValue, updatedOrder.ShipTime);
        }
#endif

        #endregion

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

