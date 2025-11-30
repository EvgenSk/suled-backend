using FluentAssertions;
using SuledFunctions.Models.Requests;
using SuledFunctions.Validators;

namespace SuledFunctions.Tests.Validators;

public class GetTournamentsQueryValidatorTests
{
    private readonly GetTournamentsQueryValidator _validator;

    public GetTournamentsQueryValidatorTests()
    {
        _validator = new GetTournamentsQueryValidator();
    }

    [Fact]
    public async Task Validate_WithValidQuery_ReturnsValid()
    {
        // Arrange
        var query = new GetTournamentsQuery
        {
            StartDateFrom = new DateTime(2025, 1, 1),
            StartDateTo = new DateTime(2025, 12, 31),
            Location = "New York",
            Division = "Pro",
            MaxResults = 50,
            Page = 1,
            PageSize = 20
        };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithMinimalQuery_ReturnsValid()
    {
        // Arrange
        var query = new GetTournamentsQuery();

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithStartDateFromAfterStartDateTo_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery
        {
            StartDateFrom = new DateTime(2025, 12, 31),
            StartDateTo = new DateTime(2025, 1, 1)
        };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "StartDateFrom");
        result.Errors[0].ErrorMessage.Should().Contain("less than or equal to");
    }

    [Fact]
    public async Task Validate_WithMaxResultsZero_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery { MaxResults = 0 };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "MaxResults");
        result.Errors[0].ErrorMessage.Should().Contain("between 1 and 500");
    }

    [Fact]
    public async Task Validate_WithMaxResultsOverLimit_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery { MaxResults = 1000 };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "MaxResults");
        result.Errors[0].ErrorMessage.Should().Contain("between 1 and 500");
    }

    [Fact]
    public async Task Validate_WithPageZero_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery { Page = 0 };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Page");
        result.Errors[0].ErrorMessage.Should().Contain("greater than 0");
    }

    [Fact]
    public async Task Validate_WithPageSizeZero_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery { PageSize = 0 };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "PageSize");
        result.Errors[0].ErrorMessage.Should().Contain("between 1 and 100");
    }

    [Fact]
    public async Task Validate_WithPageSizeOverLimit_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery { PageSize = 200 };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "PageSize");
        result.Errors[0].ErrorMessage.Should().Contain("between 1 and 100");
    }

    [Fact]
    public async Task Validate_WithLocationTooLong_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery 
        { 
            Location = new string('A', 201) // 201 characters
        };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Location");
        result.Errors[0].ErrorMessage.Should().Contain("200 characters");
    }

    [Fact]
    public async Task Validate_WithDivisionTooLong_ReturnsInvalid()
    {
        // Arrange
        var query = new GetTournamentsQuery 
        { 
            Division = new string('A', 101) // 101 characters
        };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Division");
        result.Errors[0].ErrorMessage.Should().Contain("100 characters");
    }

    [Fact]
    public async Task Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var query = new GetTournamentsQuery
        {
            StartDateFrom = new DateTime(2025, 12, 31),
            StartDateTo = new DateTime(2025, 1, 1),
            MaxResults = 0,
            Page = 0,
            PageSize = 200,
            Location = new string('A', 201),
            Division = new string('B', 101)
        };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(6);
        result.Errors.Should().Contain(e => e.PropertyName == "StartDateFrom");
        result.Errors.Should().Contain(e => e.PropertyName == "MaxResults");
        result.Errors.Should().Contain(e => e.PropertyName == "Page");
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
        result.Errors.Should().Contain(e => e.PropertyName == "Location");
        result.Errors.Should().Contain(e => e.PropertyName == "Division");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task Validate_WithValidMaxResults_ReturnsValid(int maxResults)
    {
        // Arrange
        var query = new GetTournamentsQuery { MaxResults = maxResults };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Validate_WithValidPageSize_ReturnsValid(int pageSize)
    {
        // Arrange
        var query = new GetTournamentsQuery { PageSize = pageSize };

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
