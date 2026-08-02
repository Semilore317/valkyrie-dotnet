using FluentAssertions;
using Xunit;
using Valkyrie.Orders;

namespace UnitTests;

public class LimitComparerTests
{
    // The [Fact] attribute tells the xUnit test runner that this is a unit test method.
    // The test runner will automatically locate and run any method decorated with [Fact].
    [Fact]
    public void BidLimitComparer_ShouldSortDescending()
    {
        // Arrange
        var comparer = BidLimitComparer.Comparer;
        var limitLow = new Limit(9900);   // Price of $99.00 in cents
        var limitHigh = new Limit(10000); // Price of $100.00 in cents

        // Act
        // Compare(x, y) returns a negative number if x sorts BEFORE y.
        int comparisonResult = comparer.Compare(limitHigh, limitLow);

        //  Assert
        // Since Bids must be sorted highest-to-lowest (descending), $100.00 must sort before $99.00.
        comparisonResult.Should().BeLessThan(0, "Higher Bid Prices must sort before lower Bid Prices.");
    }

    [Fact]
    public void AskLimitComparer_ShouldSortAscending()
    {
        // Arrange
        var comparer = AskLimitComparer.Comparer;
        var limit_1 = new Limit(1500);
        var limit_2 = new Limit(1600);

        // Act
        int result = comparer.Compare(limit_1, limit_2);

        // Assert
        result.Should().BeLessThan(0, "Lower ask prices must sort before higher ask prices.");
    }

    [Fact]
    public void Comparer_NullHandling_ShouldFollowStrictWeakOrdering()
    {
        // Arrange
        var comparer = BidLimitComparer.Comparer;
        var validLimit = new Limit(10000);

        // Act & Assert
        0.Should().Be(comparer.Compare(null, null));

        // Assert.True(booleanCondition) asserts that the condition evaluates to true.
        // null is less than a valid limit, so x (null) comes before y. Compare returns a negative number.
        0.Should().BeGreaterThan(comparer.Compare(null, validLimit));

        // valid limit is greater than null, so y (null) comes after x. Compare returns a positive number.
        Assert.True(comparer.Compare(validLimit, null) > 0, "A valid limit compared to null must sort last.");
        0.Should().BeLessThan(comparer.Compare(validLimit, null));
    }
}
