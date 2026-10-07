<Query Kind="Program">
  <NuGetReference>Azure.Storage.Blobs</NuGetReference>
  <Namespace>Azure.Storage.Blobs</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

using Azure.Storage.Blobs.Models;
using System.Collections.Concurrent;

// 지정한 로컬 폴더의 모든 JPG 이미지를 재귀적으로 Azure Blob Storage 컨테이너에 업로드합니다.
// 연결 문자열은 안전하게 입력받고, 원본 폴더 기준의 상대 경로를 Blob 이름으로 유지합니다.
// 같은 이름의 Blob이 있으면 덮어씁니다.
// 최대 8개 파일을 병렬 업로드하며, 진행 중인 파일과 전체 진행률을 실시간으로 표시합니다.
// 각 업로드 네트워크 작업의 제한 시간은 10분으로 설정합니다.

async Task Main()
{
    var sourcePath = "/Users/jkwchunjae/Documents/temp/project-leehyunbae";
    var containerName = "leehyunbae"; // 사용할 Blob 컨테이너 이름으로 변경하세요.

    var connectionString = Util.GetPassword("Azure Storage 연결 문자열을 입력하세요");

    ValidateSettings(sourcePath, containerName, connectionString);

    var container = await GetOrCreateContainerAsync(connectionString, containerName);
    var files = GetFilesToUpload(sourcePath).ToList();
    
    $"업로드를 시작합니다. 파일 수: {files.Count:N0}".Dump();
    
    const int maxDegreeOfParallelism = 8;
    var startedAt = Stopwatch.StartNew();
    var uploading = new ConcurrentDictionary<string, DateTime>();
    var dashboardLock = new object();
    var progressBar = new Util.ProgressBar("업로드 준비 중...").Dump();
    var dashboard = new DumpContainer().Dump("병렬 업로드 현황");
    var completedCount = 0;
    long uploadedBytes = 0;

    void RefreshDashboard()
    {
        lock (dashboardLock)
        {
            var completed = Volatile.Read(ref completedCount);
            var percent = files.Count == 0 ? 100 : completed * 100 / files.Count;

            progressBar.Percent = percent;
            progressBar.Caption =
                $"업로드: {completed:N0}/{files.Count:N0} ({percent}%) - 동시 처리: {uploading.Count}/{maxDegreeOfParallelism}";

            dashboard.Content = new
            {
                TotalFiles = files.Count,
                CompletedFiles = completed,
                RemainingFiles = files.Count - completed,
                InFlight = uploading.Count,
                MaxParallelUploads = maxDegreeOfParallelism,
                UploadedBytes = Volatile.Read(ref uploadedBytes),
                Elapsed = startedAt.Elapsed,
                UploadingNow = uploading
                    .OrderBy(x => x.Value)
                    .Select(x => new
                    {
                        BlobName = GetBlobName(sourcePath, x.Key),
                        StartedAt = x.Value,
                        Elapsed = DateTime.Now - x.Value
                    })
                    .ToList()
            };
        }
    }

    RefreshDashboard();

    await Parallel.ForEachAsync(
        files.OrderBy(x => x),
        new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
        async (filePath, cancellationToken) =>
        {
            uploading[filePath] = DateTime.Now;
            RefreshDashboard();

            try
            {
                var uploaded = await UploadFileAsync(container, sourcePath, filePath);
                Interlocked.Add(ref uploadedBytes, uploaded);
                Interlocked.Increment(ref completedCount);
            }
            finally
            {
                uploading.TryRemove(filePath, out _);
                RefreshDashboard();
            }
        });

    progressBar.Caption = $"업로드 완료: {files.Count:N0}개, {uploadedBytes:N0} bytes";
    progressBar.Percent = 100;
    RefreshDashboard();
}

void ValidateSettings(string sourcePath, string containerName, string connectionString)
{
    if (!Directory.Exists(sourcePath))
        throw new DirectoryNotFoundException($"업로드할 폴더를 찾을 수 없습니다: {sourcePath}");

    if (string.IsNullOrWhiteSpace(containerName))
        throw new ArgumentException("컨테이너 이름을 입력해야 합니다.", nameof(containerName));

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new ArgumentException("Azure Storage 연결 문자열을 입력해야 합니다.", nameof(connectionString));
}

async Task<BlobContainerClient> GetOrCreateContainerAsync(
    string connectionString,
    string containerName)
{
    var options = new BlobClientOptions();
    options.Retry.NetworkTimeout = TimeSpan.FromMinutes(10);

    var container = new BlobContainerClient(
        connectionString,
        containerName,
        options);

    await container.CreateIfNotExistsAsync();

    $"Azure 컨테이너 준비 완료: {container.Uri}".Dump();
    return container;
}

IEnumerable<string> GetFilesToUpload(string sourcePath) =>
    Directory.EnumerateFiles(
        sourcePath,
        "*.jpg",
        new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            MatchCasing = MatchCasing.CaseInsensitive
        });

async Task<long> UploadFileAsync(
    BlobContainerClient container,
    string sourcePath,
    string filePath)
{
    var blobName = GetBlobName(sourcePath, filePath);
    var fileInfo = new FileInfo(filePath);
    var blob = container.GetBlobClient(blobName);

    await using var stream = File.OpenRead(filePath);
    await blob.UploadAsync(
        stream,
        new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                CacheControl = "public, max-age=31536000",
                ContentType = "image/jpeg",
            }
        });

    return fileInfo.Length;
}

string GetBlobName(string sourcePath, string filePath)
{
    var relPath = Path.GetRelativePath(sourcePath, filePath);
    var dir = relPath.Split('/')[0];
    var name = relPath.Split('/')[1][3..].Trim();
    return $"works/large/{dir}/{name}";
}
