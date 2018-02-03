using System;
using System.Collections.Generic;

namespace Brandless.ObjectSerializer.Tests.Models
{
    public class Person
    {
        public Person(string name, int age, DateTimeOffset birthday)
        {
            Name = name;
            Age = age;
            Birthday = birthday;
        }

        public Person() { }
        public List<string> IgnoreThis { get; set; } = new List<string>();
        public DateTimeOffset Birthday { get; set; }
        public Person FavouritePerson { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public List<Address> Addresses { get; set; } = new List<Address>();
    }
}