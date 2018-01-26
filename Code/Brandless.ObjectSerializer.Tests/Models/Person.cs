using System.Collections.Generic;

namespace Brandless.ObjectSerializer.Tests.Models
{
    public class Person
    {
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public Person() { }

        public string Name { get; set; }
        public int Age { get; set; }
        public List<Address> Addresses { get; set; } = new List<Address>();
    }
}