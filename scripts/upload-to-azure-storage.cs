#!/usr/bin/env dotnet
#:package Azure.Storage.Blobs@12.30.1
#:package SkiaSharp@4.153.1
#:package SkiaSharp.NativeAssets.macOS@4.153.1

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SkiaSharp;
using System.Globalization;
using System.Text.RegularExpressions;

return await UploadProgram.RunAsync(args);

internal static class UploadProgram
{
    private const string ContainerName = "leehyunbae";
    private const int DefaultTargetBytes = 400 * 1024;
    private const int InitialLongestEdge = 2_560;
    private const int MinimumLongestEdge = 320;
    private const int MinimumJpegQuality = 30;
    private static readonly Regex PeriodPattern = new("^\\d{4}-\\d{4}$", RegexOptions.CultureInvariant);

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            if (options is null)
            {
                PrintUsage();
                return 0;
            }

            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationSource.Cancel();
            };

            var sourceFile = new FileInfo(options.SourcePath);
            ValidateSourceFile(sourceFile);

            Console.WriteLine("축소 이미지를 만드는 중입니다...");
            var thumbnail = await CreateThumbnailAsync(
                sourceFile.FullName,
                options.TargetBytes,
                cancellationSource.Token);

            var originalBlobName = $"works/large/{options.Period}/{sourceFile.Name}";
            var thumbnailFileName = $"{Path.GetFileNameWithoutExtension(sourceFile.Name)}.jpg";
            var thumbnailBlobName = $"works/small/{options.Period}/{thumbnailFileName}";

            Console.WriteLine($"원본: {sourceFile.Length:N0} bytes");
            Console.WriteLine($"축소본: {thumbnail.Bytes.Length:N0} bytes " +
                              $"({thumbnail.Width} × {thumbnail.Height}, JPEG 품질 {thumbnail.Quality})");
            Console.WriteLine($"원본 Blob: {originalBlobName}");
            Console.WriteLine($"축소본 Blob: {thumbnailBlobName}");

            if (options.DryRun)
            {
                Console.WriteLine("--dry-run 옵션으로 실행되어 Azure에 업로드하지 않았습니다.");
                return 0;
            }

            var connectionString = AzureConnectionStringFile.Read();

            var container = await GetContainerAsync(connectionString, cancellationSource.Token);
            var uploadConditions = options.Overwrite
                ? null
                : new BlobRequestConditions { IfNoneMatch = ETag.All };

            Console.WriteLine("Azure Storage에 원본을 업로드하는 중입니다...");
            var originalBlob = container.GetBlobClient(originalBlobName);
            await using (var originalStream = sourceFile.OpenRead())
            {
                await originalBlob.UploadAsync(
                    originalStream,
                    CreateUploadOptions(GetContentType(sourceFile.Extension), uploadConditions),
                    cancellationSource.Token);
            }

            Console.WriteLine("Azure Storage에 축소본을 업로드하는 중입니다...");
            var thumbnailBlob = container.GetBlobClient(thumbnailBlobName);
            await using (var thumbnailStream = new MemoryStream(thumbnail.Bytes, writable: false))
            {
                await thumbnailBlob.UploadAsync(
                    thumbnailStream,
                    CreateUploadOptions("image/jpeg", uploadConditions),
                    cancellationSource.Token);
            }

            Console.WriteLine("업로드가 완료되었습니다.");
            Console.WriteLine($"원본 URL: {originalBlob.Uri}");
            Console.WriteLine($"축소본 URL: {thumbnailBlob.Uri}");
            Console.WriteLine("다음 단계: 작품 페이지에 위 URL을 반영한 뒤 로컬 Jekyll 사이트에서 확인하세요.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("작업이 취소되었습니다.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"업로드하지 못했습니다: {exception.Message}");
            return 1;
        }
    }

    private static UploadOptions? ParseArguments(string[] args)
    {
        if (args.Length == 0 || args.Any(argument => argument is "--help" or "-h"))
        {
            return null;
        }

        string? sourcePath = null;
        string? period = null;
        var overwrite = false;
        var dryRun = false;
        var targetBytes = DefaultTargetBytes;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--period":
                    period = GetOptionValue(args, ref index, "--period");
                    break;
                case "--target-kb":
                    targetBytes = ParseTargetBytes(GetOptionValue(args, ref index, "--target-kb"));
                    break;
                case "--overwrite":
                    overwrite = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    if (args[index].StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"알 수 없는 옵션입니다: {args[index]}");
                    }

                    if (sourcePath is not null)
                    {
                        throw new ArgumentException("원본 이미지 경로는 하나만 지정할 수 있습니다.");
                    }

                    sourcePath = args[index];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("원본 이미지 경로를 지정해야 합니다.");
        }

        period ??= InferPeriodFromParentDirectory(sourcePath);
        if (string.IsNullOrWhiteSpace(period) || !PeriodPattern.IsMatch(period))
        {
            throw new ArgumentException(
                "작품 기간을 알 수 없습니다. --period 2025-2026 형식으로 지정하세요.");
        }

        return new UploadOptions(
            Path.GetFullPath(sourcePath),
            period,
            overwrite,
            dryRun,
            targetBytes);
    }

    private static string GetOptionValue(string[] args, ref int index, string optionName)
    {
        if (++index >= args.Length || args[index].StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{optionName} 옵션의 값을 지정해야 합니다.");
        }

        return args[index];
    }

    private static int ParseTargetBytes(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var targetKilobytes) ||
            targetKilobytes is < 50 or > 5_000)
        {
            throw new ArgumentException("--target-kb는 50에서 5000 사이의 정수여야 합니다.");
        }

        return checked(targetKilobytes * 1024);
    }

    private static string? InferPeriodFromParentDirectory(string sourcePath)
    {
        var parentDirectory = Directory.GetParent(Path.GetFullPath(sourcePath));
        return parentDirectory is not null && PeriodPattern.IsMatch(parentDirectory.Name)
            ? parentDirectory.Name
            : null;
    }

    private static void ValidateSourceFile(FileInfo sourceFile)
    {
        if (!sourceFile.Exists)
        {
            throw new FileNotFoundException("원본 이미지 파일을 찾을 수 없습니다.", sourceFile.FullName);
        }

        if (sourceFile.Length == 0)
        {
            throw new InvalidOperationException("빈 파일은 업로드할 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(GetContentType(sourceFile.Extension)))
        {
            throw new InvalidOperationException(
                "지원하지 않는 이미지 형식입니다. JPG, PNG, WebP, TIFF, GIF 파일을 사용하세요.");
        }
    }

    private static async Task<BlobContainerClient> GetContainerAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var clientOptions = new BlobClientOptions();
        clientOptions.Retry.NetworkTimeout = TimeSpan.FromMinutes(10);

        var container = new BlobContainerClient(connectionString, ContainerName, clientOptions);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return container;
    }

    private static BlobUploadOptions CreateUploadOptions(
        string contentType,
        BlobRequestConditions? conditions) =>
        new()
        {
            Conditions = conditions,
            HttpHeaders = new BlobHttpHeaders
            {
                CacheControl = "public, max-age=31536000",
                ContentType = contentType
            }
        };

    private static async Task<Thumbnail> CreateThumbnailAsync(
        string sourcePath,
        int targetBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var originalImage = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidOperationException("이미지 파일을 읽을 수 없습니다.");

        var originalLongestEdge = Math.Max(originalImage.Width, originalImage.Height);
        var longestEdge = Math.Min(originalLongestEdge, InitialLongestEdge);

        for (var currentLongestEdge = longestEdge; ;)
        {
            for (var quality = 90; quality >= MinimumJpegQuality; quality -= 5)
            {
                var scale = Math.Min(
                    1d,
                    currentLongestEdge / (double)Math.Max(originalImage.Width, originalImage.Height));
                var width = Math.Max(1, (int)Math.Round(originalImage.Width * scale));
                var height = Math.Max(1, (int)Math.Round(originalImage.Height * scale));

                using var candidate = originalImage.Resize(
                    new SKImageInfo(width, height),
                    new SKSamplingOptions(SKCubicResampler.Mitchell))
                    ?? throw new InvalidOperationException("축소 이미지를 만들 수 없습니다.");
                using var image = SKImage.FromBitmap(candidate);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                var bytes = data.ToArray();

                if (bytes.Length <= targetBytes)
                {
                    return new Thumbnail(bytes, candidate.Width, candidate.Height, quality);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (currentLongestEdge <= MinimumLongestEdge)
            {
                throw new InvalidOperationException(
                    $"축소 이미지를 {targetBytes / 1024:N0} KB 이하로 만들 수 없습니다. " +
                    "--target-kb 값을 늘리거나 더 작은 원본 이미지를 사용하세요.");
            }

            currentLongestEdge = Math.Max(
                MinimumLongestEdge,
                (int)Math.Floor(currentLongestEdge * 0.8));
        }
    }

    private static string GetContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".tif" or ".tiff" => "image/tiff",
            ".gif" => "image/gif",
            _ => string.Empty
        };

    private static void PrintUsage()
    {
        Console.WriteLine("""
사용법:
  dotnet run --file scripts/upload-to-azure-storage.cs -- <원본-이미지-경로> [옵션]

원본 파일의 바로 위 폴더명이 2025-2026처럼 기간 형식이면 자동으로 사용합니다.
그 외 위치의 파일은 --period 옵션을 지정해야 합니다.

옵션:
  --period <YYYY-YYYY>  Azure Blob 경로에 사용할 작품 기간
  --target-kb <KB>      축소 이미지 목표 용량 (기본값: 400)
  --overwrite           같은 Blob 이름의 기존 파일을 교체
  --dry-run             축소 이미지만 만들고 Azure에는 업로드하지 않음
  --help, -h            이 도움말 표시

연결 문자열은 환경 변수 AZURE_STORAGE_CONNECTION_STRING 또는 저장소 루트의
azure-connection-string.txt 파일에서 읽습니다. 이 파일은 Git에 포함되지 않으며,
연결 문자열만 한 줄로 작성해야 합니다.
""");
    }

    private sealed record UploadOptions(
        string SourcePath,
        string Period,
        bool Overwrite,
        bool DryRun,
        int TargetBytes);

    private sealed record Thumbnail(byte[] Bytes, int Width, int Height, int Quality);
}

internal static class AzureConnectionStringFile
{
    private const string EnvironmentVariableName = "AZURE_STORAGE_CONNECTION_STRING";

    public static string Read()
    {
        var environmentValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        var configurationPath = GetConfigurationPath();
        if (!File.Exists(configurationPath))
        {
            throw new InvalidOperationException(
                "Azure 연결 문자열 설정 파일을 찾을 수 없습니다. " +
                $"{configurationPath} 파일을 만들고 연결 문자열만 한 줄로 작성하세요. " +
                "이 파일은 .gitignore에 등록되어 GitHub에 올라가지 않습니다.");
        }

        var connectionString = File.ReadAllText(configurationPath).Trim();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{configurationPath} 파일이 비어 있습니다. Azure 연결 문자열만 한 줄로 작성하세요.");
        }

        return connectionString;
    }

    private static string GetConfigurationPath()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        {
            var scriptPath = Path.Combine(directory.FullName, "scripts", "upload-to-azure-storage.cs");
            if (File.Exists(scriptPath))
            {
                return Path.Combine(directory.FullName, "azure-connection-string.txt");
            }
        }

        return Path.Combine(Environment.CurrentDirectory, "azure-connection-string.txt");
    }
}
