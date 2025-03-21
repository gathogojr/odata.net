﻿//---------------------------------------------------------------------
// <copyright file="JsonReaderWriterInjectionTests.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Core.Tests.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.Json;
using Microsoft.Test.OData.Utils.ODataLibTest;
using Xunit;

namespace Microsoft.OData.Tests.ScenarioTests.Roundtrip
{
    public class JsonReaderWriterInjectionTests
    {
        [Fact]
        public void UseDefaultJsonWriter()
        {
            RunWriterTest(null, "{\"@odata.context\":\"http://test/$metadata#People/$entity\",\"PersonId\":999,\"Name\":\"Jack\"}");
        }

        [Fact]
        public void InjectCustomJsonWriter()
        {
            RunWriterTest(builder => builder.AddTransient<IJsonWriterFactory, TestJsonWriterFactory>(),
                "<\"@context\":\"http://test/$metadata#People/$entity\",\"PersonId\":999,\"Name\":\"Jack\",>");
        }

        [Fact]
        public void UseDefaultJsonReader()
        {
            RunReaderTest(null, "{\"@odata.context\":\"http://test/$metadata#People/$entity\",\"PersonId\":999,\"Name\":\"Jack\"}");
        }

        [Fact]
        public void UseComplexJsonReaderNestedSelect()
        {
            // $select =Address/SubAddress/SubRoad OR
            //$select =Address($select =SubAddress($select=SubRoad))

            var resDict =  RunComplexReaderTest(null, "{\"@odata.context\":\"http://test/$metadata#People(Address/SubAddress/SubRoad)/$entity\","+
            "\"Address\":{" +
                "\"SubAddress\":{\"SubRoad\":\"Redmond\"}}}");

            Assert.Equal("Redmond", Assert.IsType<ODataProperty>(resDict["DefaultNs.SubAddress"].ToList()[0]).Value);
        }
        
        [Fact]
        public void UseComplexJsonReaderExpandAndSelect()
        {
            // $expand =PersonCity($select=Zipcode),Address/City

            var resDict = RunComplexReaderTest(null, "{\"@odata.context\":\"http://test/$metadata#People(PersonCity(ZipCode),Address/City())/$entity\"," +
            "\"PersonCity\":{\"ZipCode\":10001},"+
                "\"Address\":{" +
                "\"City\":{\"ZipCode\":98052}}}");

            Assert.Equal(10001, Assert.IsType<ODataProperty>(resDict["DefaultNs.PersonCity"].ToList()[0]).Value);
            Assert.Equal(98052, Assert.IsType<ODataProperty>(resDict["DefaultNs.City"].ToList()[0]).Value);
        }

        [Fact]
        public void UseComplexJsonReaderExpandAndSelectComplex()
        {
          //$select=Address&$Expand=Address/City

            var resDict = RunComplexReaderTest(null, "{\"@odata.context\":\"http://test/$metadata#People(Address,Address/City())/$entity\"," +
             "\"Address\":{" +
                "\"SubAddress\":{\"SubRoad\":\"Redmond\"},"+
              "\"City\":{\"ZipCode\":98052}}}");

            Assert.Equal("Redmond", Assert.IsType<ODataProperty>(resDict["DefaultNs.SubAddress"].ToList()[0]).Value);
            Assert.Equal(98052, Assert.IsType<ODataProperty>(resDict["DefaultNs.City"].ToList()[0]).Value);
        }
      
        [Fact]
        public void UseComplexJsonReaderSelectComplex()
        {
            //$select=Address

            var resDict = RunComplexReaderTest(null, "{\"@odata.context\":\"http://test/$metadata#People(Address)/$entity\"," +
          "\"Address\":{" +
              "\"SubAddress\":{\"SubRoad\":\"Redmond\"}}}");

            Assert.Equal("Redmond", Assert.IsType<ODataProperty>(resDict["DefaultNs.SubAddress"].ToList()[0]).Value);
        }

        [Fact]
        public void InjectCustomJsonReader()
        {
            RunReaderTest(builder => builder.AddTransient<IJsonReaderFactory, TestJsonReaderFactory>(),
                "<\"@context\":\"http://test/$metadata#People/$entity\",\"PersonId\":999,\"Name\":\"Jack\",>");
        }

        private static void RunWriterTest(Action<IServiceCollection> action, string expectedOutput)
        {
            var resource = new ODataResource
            {
                Properties = new[]
                {
                    new ODataProperty { Name = "PersonId", Value = 999 },
                    new ODataProperty { Name = "Name", Value = "Jack" }
                }
            };
            var model = BuildModel();
            var entitySet = model.FindDeclaredEntitySet("People");
            var entityType = model.GetEntityType("NS.Person");
            var serviceProvider = ServiceProviderHelper.BuildServiceProvider(action);
            var output = GetWriterOutput(resource, model, entitySet, entityType, serviceProvider);
            Assert.Equal(expectedOutput, output);
        }

        private static void RunReaderTest(Action<IServiceCollection> action, string messageContent)
        {
            var model = BuildModel();
            var entitySet = model.FindDeclaredEntitySet("People");
            var entityType = model.GetEntityType("NS.Person");
            var serviceProvider = ServiceProviderHelper.BuildServiceProvider(action);

            var readerSettings = new ODataMessageReaderSettings
            {
                EnableReadingODataAnnotationWithoutPrefix = true
            };

            var resource = GetReadedResource(messageContent, model, entitySet, entityType, serviceProvider, readerSettings);

            var propertyList = resource.Properties.OfType<ODataProperty>().ToList();
            Assert.Equal("PersonId", propertyList[0].Name);
            Assert.Equal(999, propertyList[0].Value);
            Assert.Equal("Name", propertyList[1].Name);
            Assert.Equal("Jack", propertyList[1].Value);
        }

        private static Dictionary<string, IEnumerable<ODataPropertyInfo>> RunComplexReaderTest(Action<IServiceCollection> action, string messageContent)
        {
            var model = GetModel();
            var entitySet = model.FindDeclaredEntitySet("People");
            var entityType = model.GetEntityType("DefaultNs.Person");

            var serviceProvider = ServiceProviderHelper.BuildServiceProvider(action);
            serviceProvider.GetRequiredService<ODataMessageReaderSettings>().EnableReadingODataAnnotationWithoutPrefix = true;

            var resource = GetReadedResourceWithNestedInfo(messageContent, model, entitySet, entityType, serviceProvider);

            var resourceDict = new Dictionary<string, IEnumerable<ODataPropertyInfo>>();

            foreach(var res in resource)
            {
                resourceDict.Add(res.TypeName, res.Properties);
            }

            return resourceDict;            
        }

        private static EdmModel BuildModel()
        {
            var model = new EdmModel();

            var personType = new EdmEntityType("NS", "Person");
            personType.AddKeys(personType.AddStructuralProperty("PersonId", EdmPrimitiveTypeKind.Int32));
            personType.AddStructuralProperty("Name", EdmPrimitiveTypeKind.String);
            model.AddElement(personType);

            var container = new EdmEntityContainer("NS", "Container");
            container.AddEntitySet("People", personType);
            model.AddElement(container);

            return model;
        }

        private static EdmModel GetModel()
        {
            var model = new EdmModel();

            var person = new EdmEntityType("DefaultNs", "Person");
            var entityId = person.AddStructuralProperty("UserName", EdmCoreModel.Instance.GetString(false));
            person.AddKeys(entityId);

            var employee = new EdmEntityType("DefaultNs", "Employee", person);

            var city = new EdmEntityType("DefaultNs", "City");
            var cityId = city.AddStructuralProperty("ZipCode", EdmCoreModel.Instance.GetInt32(false));
            city.AddKeys(cityId);

            var personCity = new EdmEntityType("DefaultNs", "PersonCity");
            var personcityId = personCity.AddStructuralProperty("ZipCode", EdmCoreModel.Instance.GetInt32(false));
            personCity.AddKeys(personcityId);

            var city3 = new EdmEntityType("DefaultNs", "City3");
            var cityId3 = city.AddStructuralProperty("ZipCode3", EdmCoreModel.Instance.GetInt32(false));
            city.AddKeys(cityId3);

            var region = new EdmEntityType("DefaultNs", "Region");
            var regionId = region.AddStructuralProperty("Name", EdmCoreModel.Instance.GetString(false));
            region.AddKeys(regionId);

            var cityRegion = city.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "Region",
                Target = region,
                TargetMultiplicity = EdmMultiplicity.One,
            });

            var complex = new EdmComplexType("DefaultNs", "Address");
            complex.AddStructuralProperty("Road", EdmCoreModel.Instance.GetString(false));

            var subcomplex = new EdmComplexType("DefaultNs", "SubAddress");
            subcomplex.AddStructuralProperty("SubRoad", EdmCoreModel.Instance.GetString(false));

            complex.AddStructuralProperty("SubAddress", new EdmComplexTypeReference(subcomplex, false));

            var navP = complex.AddUnidirectionalNavigation(
                new EdmNavigationPropertyInfo()
                {
                    Name = "City",
                    Target = city,
                    TargetMultiplicity = EdmMultiplicity.One,
                });

            var navP3 = complex.AddUnidirectionalNavigation(
               new EdmNavigationPropertyInfo()
               {
                   Name = "City3",
                   Target = city3,
                   TargetMultiplicity = EdmMultiplicity.One,
               });


            var derivedComplex = new EdmComplexType("DefaultNs", "WorkAddress", complex);
            var navP2 = derivedComplex.AddUnidirectionalNavigation(
                new EdmNavigationPropertyInfo()
                {
                    Name = "City2",
                    Target = city,
                    TargetMultiplicity = EdmMultiplicity.One,
                });

            complex.AddStructuralProperty("WorkAddress", new EdmComplexTypeReference(complex, false));

            person.AddStructuralProperty("Address", new EdmComplexTypeReference(complex, false));
            person.AddStructuralProperty("Addresses", new EdmCollectionTypeReference(new EdmCollectionType(new EdmComplexTypeReference(complex, false))));
            var navP4 = person.AddUnidirectionalNavigation(
                new EdmNavigationPropertyInfo()
                {
                    Name = "PersonCity",
                    Target = personCity,
                    TargetMultiplicity = EdmMultiplicity.One,
                });
            
            model.AddElement(person);
            model.AddElement(employee);
            model.AddElement(city);
            model.AddElement(personCity);
            model.AddElement(city3);
            model.AddElement(region);
            model.AddElement(complex);
            model.AddElement(derivedComplex);

            var entityContainer = new EdmEntityContainer("DefaultNs", "Container");
            model.AddElement(entityContainer);
            EdmEntitySet people = new EdmEntitySet(entityContainer, "People", person);
            EdmEntitySet cities = new EdmEntitySet(entityContainer, "City", city);
            EdmEntitySet pcities = new EdmEntitySet(entityContainer, "PersonCity", personCity);
            EdmEntitySet regions = new EdmEntitySet(entityContainer, "Regions", region);
            people.AddNavigationTarget(navP, cities, new EdmPathExpression("Address/City"));
            people.AddNavigationTarget(navP3, cities, new EdmPathExpression("Address/City3"));
            people.AddNavigationTarget(navP, cities, new EdmPathExpression("Addresses/City"));
            people.AddNavigationTarget(navP2, cities, new EdmPathExpression("Address/WorkAddress/DefaultNs.WorkAddress/City2"));
            people.AddNavigationTarget(navP4, pcities, new EdmPathExpression("PersonCity"));


            cities.AddNavigationTarget(cityRegion, regions);
            entityContainer.AddElement(people);
            entityContainer.AddElement(cities);
            entityContainer.AddElement(pcities);

            return model;
        }


        private static string GetWriterOutput(ODataResource entry, EdmModel model, IEdmEntitySetBase entitySet, EdmEntityType entityType, IServiceProvider container)
        {
            var outputStream = new MemoryStream();
            IODataResponseMessage message = new InMemoryMessage
            {
                Stream = outputStream,
                ServiceProvider = container
            };
            message.SetHeader("Content-Type", "application/json");
            var settings = new ODataMessageWriterSettings();
            settings.SetServiceDocumentUri(new Uri("http://test"));

            using (var messageWriter = new ODataMessageWriter(message, settings, model))
            {
                var writer = messageWriter.CreateODataResourceWriter(entitySet, entityType);
                writer.WriteStart(entry);
                writer.WriteEnd();
                outputStream.Seek(0, SeekOrigin.Begin);
                return new StreamReader(outputStream).ReadToEnd();
            }
        }

        private static ODataResource GetReadedResource(string messageContent, EdmModel model, IEdmEntitySetBase entitySet, EdmEntityType entityType, IServiceProvider container, ODataMessageReaderSettings settings)
        {
            var outputStream = new MemoryStream();
            var writer = new StreamWriter(outputStream);
            writer.Write(messageContent);
            writer.Flush();
            outputStream.Seek(0, SeekOrigin.Begin);

            IODataResponseMessage message = new InMemoryMessage
            {
                Stream = outputStream,
                ServiceProvider = container
            };
            message.SetHeader("Content-Type", "application/json");

            using (var messageReader = new ODataMessageReader(message, settings, model))
            {
                var reader = messageReader.CreateODataResourceReader(entitySet, entityType);
                Assert.True(reader.Read());
                return reader.Item as ODataResource;
            }
        }

        private static IList<ODataResource> GetReadedResourceWithNestedInfo(string messageContent, EdmModel model, IEdmEntitySetBase entitySet, EdmEntityType entityType, IServiceProvider container)
        {
            var outputStream = new MemoryStream();
            var writer = new StreamWriter(outputStream);
            writer.Write(messageContent);
            writer.Flush();
            outputStream.Seek(0, SeekOrigin.Begin);

            IODataResponseMessage message = new InMemoryMessage
            {
                Stream = outputStream,
                ServiceProvider = container
            };
            message.SetHeader("Content-Type", "application/json;odata.metadata=full");

            var settings = new ODataMessageReaderSettings();
            using (var messageReader = new ODataMessageReader(message, settings, model))
            {
                var reader = messageReader.CreateODataResourceReader(entitySet, entityType);
                IList<ODataResource> resources = new List<ODataResource>();

                while (reader.Read())
                {
                    if (reader.State == ODataReaderState.ResourceEnd)
                    {
                        resources.Add(reader.Item as ODataResource);
                    }
                }

                return resources;
            }
        }

        private class TestJsonWriterFactory : IJsonWriterFactory
        {
            public IJsonWriter CreateJsonWriter(Stream stream, bool isIeee754Compatible, Encoding encoding)
            {
                var textWriter = new StreamWriter(stream);

                return new TestJsonWriter(textWriter);
            }
        }

        private class TestJsonReaderFactory : IJsonReaderFactory
        {
            public IJsonReader CreateJsonReader(TextReader textReader, bool isIeee754Compatible)
            {
                return new TestJsonReader();
            }
        }

        private class TestJsonWriter : IJsonWriter
        {
            private readonly TextWriter textWriter;

            public TestJsonWriter(TextWriter textWriter)
            {
                this.textWriter = textWriter;
            }

            public void StartPaddingFunctionScope() => throw new NotImplementedException();

            public void EndPaddingFunctionScope() => throw new NotImplementedException();

            public void StartObjectScope()
            {
                this.textWriter.Write('<');
            }

            public void EndObjectScope()
            {
                this.textWriter.Write('>');
            }

            public void StartArrayScope() => throw new NotImplementedException();

            public void EndArrayScope() => throw new NotImplementedException();

            public void WriteName(string name)
            {
                if (name.StartsWith("@odata."))
                {
                    name = '@' + name.Substring(7);
                }

                this.textWriter.Write("\"{0}\":", name);
            }

            public void WritePaddingFunctionName(string functionName) => throw new NotImplementedException();

            public void WriteValue(bool value) => throw new NotImplementedException();

            public void WriteValue(int value)
            {
                this.textWriter.Write("{0},", value);
            }

            public void WriteValue(float value) => throw new NotImplementedException();

            public void WriteValue(short value) => throw new NotImplementedException();

            public void WriteValue(long value) => throw new NotImplementedException();

            public void WriteValue(double value) => throw new NotImplementedException();

            public void WriteValue(Guid value) => throw new NotImplementedException();

            public void WriteValue(decimal value) => throw new NotImplementedException();

            public void WriteValue(DateTimeOffset value) => throw new NotImplementedException();

            public void WriteValue(TimeSpan value) => throw new NotImplementedException();

            public void WriteValue(byte value) => throw new NotImplementedException();

            public void WriteValue(sbyte value) => throw new NotImplementedException();

            public void WriteValue(string value)
            {
                this.textWriter.Write("\"{0}\",", value);
            }

            public void WriteValue(byte[] value) => throw new NotImplementedException();

            public void WriteValue(Date value) => throw new NotImplementedException();

            public void WriteValue(TimeOfDay value) => throw new NotImplementedException();

            public void WriteValue(System.Text.Json.JsonElement value) => throw new NotImplementedException();

            public void WriteRawValue(string rawValue) => throw new NotImplementedException();

            public void Flush()
            {
                this.textWriter.Flush();
            }

            public Task StartPaddingFunctionScopeAsync() => throw new NotImplementedException();

            public Task EndPaddingFunctionScopeAsync() => throw new NotImplementedException();

            public Task StartObjectScopeAsync()
            {
                return this.textWriter.WriteAsync('<');
            }

            public Task EndObjectScopeAsync()
            {
                return this.textWriter.WriteAsync('>');
            }

            public Task StartArrayScopeAsync() => throw new NotImplementedException();

            public Task EndArrayScopeAsync() => throw new NotImplementedException();

            public async Task WriteNameAsync(string name)
            {
                if (name.StartsWith("@odata."))
                {
                    name = '@' + name.Substring(7);
                }

                await this.textWriter.WriteAsync(string.Format("\"{0}\":", name));
            }

            public Task WritePaddingFunctionNameAsync(string functionName) => throw new NotImplementedException();

            public Task WriteValueAsync(bool value) => throw new NotImplementedException();

            public Task WriteValueAsync(int value)
            {
                return this.textWriter.WriteAsync(string.Format("{0},", value));
            }

            public Task WriteValueAsync(float value) => throw new NotImplementedException();

            public Task WriteValueAsync(short value) => throw new NotImplementedException();

            public Task WriteValueAsync(long value) => throw new NotImplementedException();

            public Task WriteValueAsync(double value) => throw new NotImplementedException();

            public Task WriteValueAsync(Guid value) => throw new NotImplementedException();

            public Task WriteValueAsync(decimal value) => throw new NotImplementedException();

            public Task WriteValueAsync(DateTimeOffset value) => throw new NotImplementedException();

            public Task WriteValueAsync(TimeSpan value) => throw new NotImplementedException();

            public Task WriteValueAsync(byte value) => throw new NotImplementedException();

            public Task WriteValueAsync(sbyte value) => throw new NotImplementedException();

            public Task WriteValueAsync(string value)
            {
                return this.textWriter.WriteAsync(string.Format("\"{0}\",", value));
            }

            public Task WriteValueAsync(byte[] value) => throw new NotImplementedException();

            public Task WriteValueAsync(Date value) => throw new NotImplementedException();

            public Task WriteValueAsync(TimeOfDay value) => throw new NotImplementedException();

            public Task WriteRawValueAsync(string rawValue) => throw new NotImplementedException();

            public Task FlushAsync()
            {
                return this.textWriter.FlushAsync();
            }

            public Task WriteValueAsync(JsonElement value) => throw new NotImplementedException();

            public Stream StartStreamValueScope() => throw new NotImplementedException();

            public TextWriter StartTextWriterValueScope(string contentType) => throw new NotImplementedException();

            public void EndStreamValueScope() => throw new NotImplementedException();

            public void EndTextWriterValueScope() => throw new NotImplementedException();

            public Task<Stream> StartStreamValueScopeAsync() => throw new NotImplementedException();

            public Task<TextWriter> StartTextWriterValueScopeAsync(string contentType) => throw new NotImplementedException();

            public Task EndStreamValueScopeAsync() => throw new NotImplementedException();

            public Task EndTextWriterValueScopeAsync() => throw new NotImplementedException();
        }

        private class TestJsonReader : IJsonReader
        {
            private int callCount;
            private object value;
            private JsonNodeType nodeType = JsonNodeType.None;

            public object GetValue()
            {
                return this.value;
            }

            public JsonNodeType NodeType
            {
                get { return this.nodeType; }
            }

            public bool IsIeee754Compatible
            {
                get { return true; }
            }

            public bool Read()
            {
                switch (callCount)
                {
                    case 0:
                        value = null;
                        nodeType = JsonNodeType.StartObject;
                        break;
                    case 1:
                        value = "@context";
                        nodeType = JsonNodeType.Property;
                        break;
                    case 2:
                        value = "http://test/$metadata#People/$entity";
                        nodeType = JsonNodeType.PrimitiveValue;
                        break;
                    case 3:
                        value = "PersonId";
                        nodeType = JsonNodeType.Property;
                        break;
                    case 4:
                        value = 999;
                        nodeType = JsonNodeType.PrimitiveValue;
                        break;
                    case 5:
                        value = "Name";
                        nodeType = JsonNodeType.Property;
                        break;
                    case 6:
                        value = "Jack";
                        nodeType = JsonNodeType.PrimitiveValue;
                        break;
                    case 7:
                        value = null;
                        nodeType = JsonNodeType.EndObject;
                        break;
                    case 8:
                        value = null;
                        nodeType = JsonNodeType.EndOfInput;
                        return false;
                    default:
                        return false;
                }

                ++callCount;
                return true;
            }

            public Task<bool> ReadAsync() => throw new NotImplementedException();

            public bool CanStream() => throw new NotImplementedException();

            public Task<bool> CanStreamAsync() => throw new NotImplementedException();

            public Stream CreateReadStream() => throw new NotImplementedException();

            public Task<Stream> CreateReadStreamAsync() => throw new NotImplementedException();

            public TextReader CreateTextReader() => throw new NotImplementedException();

            public Task<TextReader> CreateTextReaderAsync() => throw new NotImplementedException();

            public Task<object> GetValueAsync() => throw new NotImplementedException();
        }
    }
}
