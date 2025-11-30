using FluentValidation;

namespace SuledFunctions.Validators;

/// <summary>
/// Validator for file upload streams
/// </summary>
public class FileUploadValidator : AbstractValidator<Stream>
{
    private readonly long _maxSizeBytes;
    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };

    public FileUploadValidator(long maxSizeBytes)
    {
        _maxSizeBytes = maxSizeBytes;

        RuleFor(stream => stream)
            .NotNull()
            .WithMessage("File stream cannot be null");

        RuleFor(stream => stream.Length)
            .GreaterThan(0)
            .WithMessage("File cannot be empty")
            .LessThanOrEqualTo(_maxSizeBytes)
            .WithMessage($"File size cannot exceed {_maxSizeBytes / (1024 * 1024)} MB");
    }

    /// <summary>
    /// Validates file extension
    /// </summary>
    public static bool ValidateFileExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedExtensions.Contains(extension);
    }
}
