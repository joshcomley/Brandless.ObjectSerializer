using System.Collections.Generic;
using System.IO;
using Brandless.ObjectSerializer.Tests.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Brandless.ObjectSerializer.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var list = new List<Person>();
            var person1 = new Person("Paulina", 24);
            var person2 = new Person("Josh", 33);
            var person3 = new Person("Bob", 33);
            person1.Addresses.Add(new Address("My house", "AB1 C23"));
            person1.Addresses.Add(new Address("My other house", "DE2 F34"));
            person2.Addresses.Add(new Address("Big Place", "XY4 Z56"));
            person3.Addresses = null;
            list.Add(person1);
            list.Add(person2);
            list.Add(person3);
            //var options = new CSharpSerializeToClassParameters("InMemoryDb");
            var options = new CSharpSerializeToObjectParameters();
            var serializer = new CSharpObjectSerializer(options);
            var code = serializer.Serialize(list);
            File.WriteAllText(@"d:\code\temp.cs", code);
        }
    }
}
