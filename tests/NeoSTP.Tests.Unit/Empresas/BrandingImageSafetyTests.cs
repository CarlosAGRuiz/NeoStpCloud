using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Branding;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using SkiaSharp;

namespace NeoSTP.Tests.Unit.Empresas;

public class BrandingImageSafetyTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpg")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public void CompleteSupportedImagesAreAccepted(SKEncodedImageFormat format, string mime)
    {
        var bytes = Image(format, 80, 40);
        BrandingImageValidator.TryValidate(bytes, mime, out var detected, out var error).Should().BeTrue(error);
        detected.Should().Be(format == SKEncodedImageFormat.Jpeg ? "image/jpeg" : $"image/{format.ToString().ToLowerInvariant()}");
    }

    [Theory]
    [InlineData("NOT AN IMAGE", "image/png")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'></svg>", "image/png")]
    [InlineData("%PDF-1.7", "image/jpeg")]
    public void DeclaredMimeNeverAuthorizesNonRasterBytes(string contents, string mime)
        => BrandingImageValidator.TryValidate(System.Text.Encoding.UTF8.GetBytes(contents), mime, out _, out _).Should().BeFalse();

    [Fact]
    public void TruncatedImageIsRejectedByStructureOrDecoder()
    {
        var image = Image(SKEncodedImageFormat.Png, 80, 40);
        BrandingImageValidator.TryValidate(image[..(image.Length / 2)], "image/png", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void MimeMismatchIsRejected()
        => BrandingImageValidator.TryValidate(Image(SKEncodedImageFormat.Png, 80, 40), "image/jpeg", out _, out _).Should().BeFalse();

    [Fact]
    public void ApngContainerIsRejectedBeforeDecode()
    {
        var png = Image(SKEncodedImageFormat.Png, 80, 40);
        var apng = new byte[png.Length + 12];
        png.AsSpan(0, 33).CopyTo(apng);
        "acTL"u8.CopyTo(apng.AsSpan(37, 4));
        png.AsSpan(33).CopyTo(apng.AsSpan(45));
        BrandingImageValidator.TryValidate(apng, "image/png", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void AnimatedWebpContainerIsRejectedBeforeDecode()
    {
        var webp = Image(SKEncodedImageFormat.Webp, 80, 40);
        "ANIM"u8.CopyTo(webp.AsSpan(12, 4));
        BrandingImageValidator.TryValidate(webp, "image/webp", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void OversizedJpegHeaderIsRejectedBeforeDecode()
    {
        var jpeg = Image(SKEncodedImageFormat.Jpeg, 1, 1);
        var sof = FindJpegSof(jpeg);
        WriteBigEndian(jpeg.AsSpan(sof + 7, 2), 4097);
        BrandingImageValidator.TryValidate(jpeg, "image/jpeg", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void OversizedWebpHeaderIsRejectedBeforeDecode()
    {
        var webp = new byte[30];
        "RIFF"u8.CopyTo(webp); WriteLittleEndian(webp.AsSpan(4, 4), 22); "WEBP"u8.CopyTo(webp.AsSpan(8));
        "VP8 "u8.CopyTo(webp.AsSpan(12)); WriteLittleEndian(webp.AsSpan(16, 4), 10);
        webp[23] = 0x9d; webp[24] = 1; webp[25] = 0x2a;
        webp[26] = 0x01; webp[27] = 0x10; webp[28] = 1;
        BrandingImageValidator.TryValidate(webp, "image/webp", out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(4097, 1)]
    [InlineData(2001, 2000)]
    public void OversizedPixelDimensionsAreRejectedBeforeDecode(int width, int height)
    {
        var header = Image(SKEncodedImageFormat.Png, 1, 1);
        WriteBigEndian(header.AsSpan(16, 4), width); WriteBigEndian(header.AsSpan(20, 4), height);
        BrandingImageValidator.TryValidate(header, "image/png", out _, out _).Should().BeFalse();
    }

    [Fact]
    public async Task RejectedUploadDoesNotOverwriteExistingLogoOrAuditSuccess()
    {
        await using var db = Db();
        var existing = Image(SKEncodedImageFormat.Png, 40, 20);
        var company = new Empresa { Id = 20, Nit = "SYNTHETIC", RazonSocial = "SYNTHETIC", LogoBlob = existing, LogoContentType = "image/png" };
        db.Empresas.Add(company); await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditoriaService>(); var service = new BrandingService(db, audit);
        var result = await service.GuardarLogoAsync(20, "fake"u8.ToArray(), "image/png", "logo.png", "audit");
        result.IsFailure.Should().BeTrue(); result.ErrorCode.Should().Be("VALIDATION");
        company.LogoBlob.Should().BeEquivalentTo(existing); company.LogoContentType.Should().Be("image/png");
        audit.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task LegacyInvalidLogoIsNotServedOrAdvertised()
    {
        await using var db = Db();
        db.Empresas.Add(new Empresa { Id = 20, Nit = "SYNTHETIC", RazonSocial = "SYNTHETIC",
            LogoBlob = "not-an-image"u8.ToArray(), LogoContentType = "image/png" });
        await db.SaveChangesAsync();
        var service = new BrandingService(db, Substitute.For<IAuditoriaService>());
        (await service.GetLogoAsync(20)).Should().BeNull();
        var branding = await service.GetAsync(20);
        branding.TieneLogo.Should().BeFalse(); branding.LogoContentType.Should().BeNull();
        (await service.GetLogoAsync(21)).Should().BeNull("tenant isolation remains explicit");
    }

    internal static byte[] Image(SKEncodedImageFormat format, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height)); surface.Canvas.Clear(SKColors.Purple);
        using var image = surface.Snapshot(); using var data = image.Encode(format, 90); return data.ToArray();
    }
    private static NeoStpDbContext Db() => new(new DbContextOptionsBuilder<NeoStpDbContext>()
        .UseInMemoryDatabase("branding-safety-" + Guid.NewGuid()).Options);
    private static void WriteBigEndian(Span<byte> target, int value)
    {
        if (target.Length == 2) { target[0] = (byte)(value >> 8); target[1] = (byte)value; return; }
        target[0] = (byte)(value >> 24); target[1] = (byte)(value >> 16); target[2] = (byte)(value >> 8); target[3] = (byte)value;
    }
    private static void WriteLittleEndian(Span<byte> target, int value)
    {
        target[0] = (byte)value; target[1] = (byte)(value >> 8); target[2] = (byte)(value >> 16); target[3] = (byte)(value >> 24);
    }
    private static int FindJpegSof(byte[] jpeg)
    {
        for (var i = 2; i < jpeg.Length - 9; i++)
            if (jpeg[i] == 0xff && jpeg[i + 1] is >= 0xc0 and <= 0xcf and not (0xc4 or 0xc8 or 0xcc)) return i;
        throw new InvalidOperationException("Synthetic JPEG has no SOF marker.");
    }
}
