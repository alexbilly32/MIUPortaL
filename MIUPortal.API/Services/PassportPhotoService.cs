using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace MIUPortal.API.Services
{
    public class PassportPhotoService
    {
        public async Task<string> ResizePassportPhoto(
            IFormFile file,
            string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string fileName = $"{Guid.NewGuid()}.jpg";
            string fullPath = Path.Combine(folderPath, fileName);

            using Stream input = file.OpenReadStream();

            using SKBitmap original = SKBitmap.Decode(input)
                ?? throw new Exception("Invalid image.");

            using SKBitmap resized = original.Resize(
                new SKImageInfo(300, 300),
                SKSamplingOptions.Default)
                ?? throw new Exception("Unable to resize image.");

            using SKImage image = SKImage.FromBitmap(resized);

            using SKData data = image.Encode(
                SKEncodedImageFormat.Jpeg,
                75);

            await File.WriteAllBytesAsync(fullPath, data.ToArray());

            return fileName;
        }
    }
}