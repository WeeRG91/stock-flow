using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class SupplierLengthTests
{
    // Total length: 193 + finalLabelLength.
    // Each domain label remains within 63 characters.
    private static string LongEmail(int finalLabelLength) =>
        new string('a', 64) + "@" +
        new string('b', 63) + "." +
        new string('c', 63) + "." +
        new string('d', finalLabelLength);

    [Fact]
    public void Constructor_AcceptsMaximumLengthsAfterTrimming()
    {
        var code = new string('A', Supplier.MaxCodeLength);
        var name = new string('N', Supplier.MaxNameLength);
        var email = LongEmail(61);
        var phone = new string('1', Supplier.MaxPhoneNumberLength);

        Assert.Equal(Supplier.MaxContactEmailLength, email.Length);

        var supplier = new Supplier(
            $"  {code}  ",
            $"  {name}  ",
            $"  {email}  ",
            $"  {phone}  ");

        Assert.Equal(code, supplier.Code);
        Assert.Equal(name, supplier.Name);
        Assert.Equal(email, supplier.ContactEmail);
        Assert.Equal(phone, supplier.PhoneNumber);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("contactEmail")]
    [InlineData("phoneNumber")]
    public void Constructor_RejectsOneCharacterOverMaximum(string field)
    {
        var code = field == "code"
            ? new string('A', Supplier.MaxCodeLength + 1)
            : "SUP-001";

        var name = field == "name"
            ? new string('N', Supplier.MaxNameLength + 1)
            : "Supplier";

        var email = field == "contactEmail"
            ? LongEmail(62)
            : "sales@example.com";

        var phone = field == "phoneNumber"
            ? new string('1', Supplier.MaxPhoneNumberLength + 1)
            : null;

        var exception = Assert.Throws<ArgumentException>(
            () => new Supplier(code, name, email, phone));

        Assert.Equal(field, exception.ParamName);
    }
}