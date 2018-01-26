namespace Brandless.ObjectSerializer.Tests.Models
{
    public class Address
    {
        public Address() { }

        public Address(string street, string postCode)
        {
            Street = street;
            PostCode = postCode;
        }

        public string Street { get; set; }
        public string PostCode { get; set; }
    }
}