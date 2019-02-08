// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Xml.Serialization;
using FluentAssertions;
using Sloos.Pump.SsdlSchema.Xsd;
using Sloos.Pump.Test.Samples;
using Xunit;

namespace Sloos.Pump.Test.EntityFramework.Xsd
{
    public class DeserializeAllClrTypesTest
    {
        [Fact]
        public void Deserialize()
        {
            var serializer = new XmlSerializer(typeof(TSchema));
            var schema = (TSchema)serializer.Deserialize(CodeFirstGen.AllClrTypes.Ssdl());

            schema.Should().NotBeNull();
        }
    }
}
