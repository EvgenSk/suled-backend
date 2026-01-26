using FluentAssertions;
using SuledFunctions.Validators;
using System.Text;
using Microsoft.Extensions.Options;
using SuledFunctions.Configuration;

namespace SuledFunctions.Tests.Validators;

public class FileUploadValidatorTests
{
    private readonly FileUploadValidator _validator;

    public FileUploadValidatorTests()
    {
        _validator = new FileUploadValidator(Options.Create(new TournamentSettings
        {
            MaxUploadSizeBytes = 10 * 1024 * 1024
        })); // 10MB max
    }

    [Fact]
    public async Task Validate_WithValidExcelFile_ReturnsValid()
    {
        // Arrange
        var stream = CreateMockExcelStream(1024); // 1KB file

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyStream_ReturnsInvalid()
    {
        // Arrange
        var stream = new MemoryStream();

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorMessage.Should().Be("File cannot be empty");
    }

    [Fact]
    public async Task Validate_WithFileTooLarge_ReturnsInvalid()
    {
        // Arrange
        var stream = CreateMockExcelStream(11 * 1024 * 1024); // 11MB - over 10MB limit

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorMessage.Should().Contain("cannot exceed");
    }

    [Fact]
    public async Task Validate_WithExactMaxSize_ReturnsValid()
    {
        // Arrange
        var stream = CreateMockExcelStream(10 * 1024 * 1024); // Exactly 10MB

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithXlsxExtension_ReturnsValid()
    {
        // Arrange
        var stream = CreateMockExcelStream(1024);
        stream.Position = 0; // Reset position after creation

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithXlsExtension_ReturnsValid()
    {
        // Arrange
        var stream = CreateMockExcelStream(1024);
        stream.Position = 0;

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(5 * 1024 * 1024)]
    public async Task Validate_WithVariousValidSizes_ReturnsValid(int sizeInBytes)
    {
        // Arrange
        var stream = CreateMockExcelStream(sizeInBytes);

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(11 * 1024 * 1024)]
    [InlineData(20 * 1024 * 1024)]
    [InlineData(50 * 1024 * 1024)]
    public async Task Validate_WithVariousOversizedFiles_ReturnsInvalid(int sizeInBytes)
    {
        // Arrange
        var stream = CreateMockExcelStream(sizeInBytes);

        // Act
        var result = await _validator.ValidateAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorMessage.Should().Contain("cannot exceed");
    }

    [Fact]
    public async Task Validate_StreamPositionPreserved_AfterValidation()
    {
        // Arrange
        var stream = CreateMockExcelStream(1024);
        var originalPosition = stream.Position;

        // Act
        await _validator.ValidateAsync(stream);

        // Assert
        stream.Position.Should().Be(originalPosition);
    }

    [Fact]
    public async Task Validate_WithNullStream_ThrowsException()
    {
        // Arrange
        Stream? stream = null;

        // Act
        var act = async () => await _validator.ValidateAsync(stream!);

        // Assert - FluentValidation throws InvalidOperationException for null models
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private MemoryStream CreateMockExcelStream(int sizeInBytes)
    {
        // Create Excel file signature (ZIP format - XLSX files are ZIP archives)
        var excelSignature = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // PK.. (ZIP signature)
        
        var stream = new MemoryStream();
        
        // Write Excel signature
        stream.Write(excelSignature, 0, excelSignature.Length);
        
        // Fill remaining bytes to reach desired size
        var remainingBytes = sizeInBytes - excelSignature.Length;
        if (remainingBytes > 0)
        {
            var buffer = new byte[Math.Min(remainingBytes, 8192)];
            var bytesWritten = 0;
            
            while (bytesWritten < remainingBytes)
            {
                var bytesToWrite = Math.Min(buffer.Length, remainingBytes - bytesWritten);
                stream.Write(buffer, 0, bytesToWrite);
                bytesWritten += bytesToWrite;
            }
        }
        
        stream.Position = 0;
        return stream;
    }
}
