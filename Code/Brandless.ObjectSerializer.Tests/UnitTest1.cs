using System.Collections.Generic;
using Brandless.ObjectSerializer.Tests.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Brandless.ObjectSerializer.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void IntegerValue()
        {
            var serializer = new CSharpObjectSerializer();
            var code = serializer.Serialize(789);
            Assert.AreEqual(@"var instance = 789;", code.Instance);
        }

        [TestMethod]
        public void LongValue()
        {
            var serializer = new CSharpObjectSerializer();
            var code = serializer.Serialize(789L);
            Assert.AreEqual(@"var instance = 789L;", code.Instance);
        }

        [TestMethod]
        public void SimpleTest()
        {
            var list = GetSimpleList();
            var serializer = new CSharpObjectSerializer();
            var code = serializer.Serialize(list).Initialiser;
            Assert.AreEqual(@"new List<Person>
{
    new Person
    {
        Name = ""Paulina"",
        Age = 24,
        Addresses = new List<Address>
        {
            new Address
            {
                Street = ""My house"",
                PostCode = ""AB1 C23""
            },
            new Address
            {
                Street = ""My other house"",
                PostCode = ""DE2 F34""
            }
        }
    },
    new Person
    {
        Name = ""Josh"",
        Age = 33,
        Addresses = new List<Address>
        {
            new Address
            {
                Street = ""Big Place"",
                PostCode = ""XY4 Z56""
            }
        }
    },
    new Person
    {
        Name = ""Bob"",
        Age = 33
    }
}", code);
        }

        [TestMethod]
        public void InterceptNullTest()
        {
            var person = new Person();
            person.Age = 47;
            var serializer = new CSharpObjectSerializer();
            serializer.Converters.Add(new NameConverter());
            var code = serializer.Serialize(person).Initialiser;
            Assert.AreEqual(@"new Person
{
    Name = "" (age: 47)"",
    Age = 47,
    Addresses = new List<Address>()
}", code);
        }

        [TestMethod]
        public void InterceptTest()
        {
            var list = GetSimpleList();
            var serializer = new CSharpObjectSerializer();
            serializer.Converters.Add(new NameConverter());
            var code = serializer.Serialize(list).Initialiser;
            Assert.AreEqual(@"new List<Person>
{
    new Person
    {
        Name = ""Paulina (age: 24)"",
        Age = 24,
        Addresses = new List<Address>
        {
            new Address
            {
                Street = ""My house"",
                PostCode = ""AB1 C23""
            },
            new Address
            {
                Street = ""My other house"",
                PostCode = ""DE2 F34""
            }
        }
    },
    new Person
    {
        Name = ""Josh (age: 33)"",
        Age = 33,
        Addresses = new List<Address>
        {
            new Address
            {
                Street = ""Big Place"",
                PostCode = ""XY4 Z56""
            }
        }
    },
    new Person
    {
        Name = ""Bob (age: 33)"",
        Age = 33
    }
}", code);
        }

        [TestMethod]
        public void TestComments()
        {
            var list = GetSimpleList();
            var options = new CSharpSerializerParameters();
            options.DescriptionFormatter.SetForType<List<Person>>(
                (list2, args) => { return "Hello"; });
            var serializer = new CSharpObjectSerializer(options);
            options.BeforeInstanceComment = "Before instance comment";
            options.BeforeInitialiserComment = "Before initialiser comment";
            options.AfterInstanceComment = "After instance comment";
            options.AfterInitialiserComment = "After initialiser comment";
            var code = serializer.Serialize(list).Class;
            Assert.AreEqual(@"using Brandless.ObjectSerializer.Tests.Models;
using System;
using System.Collections.Generic;
public class SerializedObject
{
    public List<Person>GetData()
    {
        // Before instance comment
        var instance = /* Before initialiser comment */ new List<Person>
        {
            new Person
            {
                Name = ""Paulina"",
                Age = 24,
                Addresses = new List<Address>
                {
                    new Address
                    {
                        Street = ""My house"",
                        PostCode = ""AB1 C23""
                    },
                    new Address
                    {
                        Street = ""My other house"",
                        PostCode = ""DE2 F34""
                    }
                }
            },
            new Person
            {
                Name = ""Josh"",
                Age = 33,
                Addresses = new List<Address>
                {
                    new Address
                    {
                        Street = ""Big Place"",
                        PostCode = ""XY4 Z56""
                    }
                }
            },
            new Person
            {
                Name = ""Bob"",
                Age = 33
            }
        } /* After initialiser comment */ ;
        // After instance comment
        return instance;
    }
}", code);
        }

        private static List<Person> GetSimpleList()
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
            return list;
        }
    }
}
