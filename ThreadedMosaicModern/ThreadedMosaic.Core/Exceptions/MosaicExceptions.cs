using System;

namespace ThreadedMosaic.Core.Exceptions
{
    /// <summary>
    /// Base exception for all mosaic-related errors
    /// </summary>
    public abstract class MosaicException : Exception
    {
        protected MosaicException(string message) : base(message) { }
        protected MosaicException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when image processing fails
    /// </summary>
    public class ImageProcessingException : MosaicException
    {
        public string? ImagePath { get; }

        public ImageProcessingException(string message, string? imagePath = null) 
            : base(message)
        {
            ImagePath = imagePath;
        }

        public ImageProcessingException(string message, Exception innerException, string? imagePath = null) 
            : base(message, innerException)
        {
            ImagePath = imagePath;
        }
    }

    /// <summary>
    /// Exception thrown when file operations fail
    /// </summary>
    public class FileOperationException : MosaicException
    {
        public string? FilePath { get; }
        public string? Operation { get; }

        public FileOperationException(string message, string? filePath = null, string? operation = null) 
            : base(message)
        {
            FilePath = filePath;
            Operation = operation;
        }

        public FileOperationException(string message, Exception innerException, string? filePath = null, string? operation = null) 
            : base(message, innerException)
        {
            FilePath = filePath;
            Operation = operation;
        }
    }

    /// <summary>
    /// Exception thrown when mosaic creation fails
    /// </summary>
    public class MosaicCreationException : MosaicException
    {
        public Guid? MosaicId { get; }
        public string? MosaicType { get; }

        public MosaicCreationException(string message, Guid? mosaicId = null, string? mosaicType = null) 
            : base(message)
        {
            MosaicId = mosaicId;
            MosaicType = mosaicType;
        }

        public MosaicCreationException(string message, Exception innerException, Guid? mosaicId = null, string? mosaicType = null) 
            : base(message, innerException)
        {
            MosaicId = mosaicId;
            MosaicType = mosaicType;
        }
    }

    /// <summary>
    /// Exception thrown when validation fails
    /// </summary>
    public class ValidationException : MosaicException
    {
        public string[] ValidationErrors { get; }

        public ValidationException(string message, params string[] validationErrors) 
            : base(message)
        {
            ValidationErrors = validationErrors ?? Array.Empty<string>();
        }

        public ValidationException(string[] validationErrors) 
            : base($"Validation failed: {string.Join(", ", validationErrors)}")
        {
            ValidationErrors = validationErrors ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Exception thrown when insufficient resources are available
    /// </summary>
    public class InsufficientResourcesException : MosaicException
    {
        public string? ResourceType { get; }
        public long? RequiredAmount { get; }
        public long? AvailableAmount { get; }

        public InsufficientResourcesException(
            string message, 
            string? resourceType = null, 
            long? requiredAmount = null, 
            long? availableAmount = null) 
            : base(message)
        {
            ResourceType = resourceType;
            RequiredAmount = requiredAmount;
            AvailableAmount = availableAmount;
        }
    }

    /// <summary>
    /// Exception thrown when an unsupported image format is encountered
    /// </summary>
    public class UnsupportedImageFormatException : MosaicException
    {
        public string? ImagePath { get; }
        public string? Format { get; }

        public UnsupportedImageFormatException(string message, string? imagePath = null, string? format = null) 
            : base(message)
        {
            ImagePath = imagePath;
            Format = format;
        }
    }

    /// <summary>
    /// Exception thrown when mosaic processing is cancelled
    /// </summary>
    public class MosaicCancelledException : MosaicException
    {
        public Guid? MosaicId { get; }

        public MosaicCancelledException(string message, Guid? mosaicId = null) 
            : base(message)
        {
            MosaicId = mosaicId;
        }
    }
}