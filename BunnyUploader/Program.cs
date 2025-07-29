using BunnyCDN.Net.Storage;
using BunnyUploader;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

const int MaxAttempts = 3;

var storageZone = Environment.GetEnvironmentVariable("INPUT_STORAGE_ZONE");
var apiKey = Environment.GetEnvironmentVariable("INPUT_API_KEY");
var localPath = Environment.GetEnvironmentVariable("INPUT_LOCAL_PATH");
var remotePath = Environment.GetEnvironmentVariable("INPUT_REMOTE_PATH") ?? "";
bool removeOldFiles = Convert.ToBoolean(Environment.GetEnvironmentVariable("INPUT_REMOVE_OLD_FILES"));
bool purgeAfterUpload = Convert.ToBoolean(Environment.GetEnvironmentVariable("INPUT_PURGE_AFTER_UPLOAD"));


if (string.IsNullOrEmpty(storageZone))
{
    Console.Error.WriteLine("Error: The 'storage-zone' field is required.");
    return 1;
}

if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("Error: The 'api-key' field is required.");
    return 1;
}

if (string.IsNullOrEmpty(localPath))
{
    Console.Error.WriteLine("Error: The 'local-path' field is required.");
    return 1;
}

var fullPath = Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? "./", localPath));
if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
{
    Console.Error.WriteLine($"Error: The local path '{fullPath}' was not found.");
    return 1;
}

var bunnyStorageZone = await GetBunnyPullZone(storageZone, apiKey);

var bunnyCDNStorage = new BunnyCDNStorage(bunnyStorageZone.Name, bunnyStorageZone.Password, bunnyStorageZone.Region);

var options = new ParallelOptions()
{
    MaxDegreeOfParallelism = 50 //https://docs.bunny.net/reference/edge-storage-api-limits
};

ConcurrentBag<string> deletedFiles = [];
ConcurrentBag<string> deletedFilesFail = [];

if (removeOldFiles)
{
    var bunnyFiles = await bunnyCDNStorage.GetStorageObjectsAsync(bunnyStorageZone.Name + "/");

    Console.WriteLine($"{bunnyStorageZone.Name}: {bunnyFiles.Count} files");
    Console.WriteLine($"Removing old files from Bunny {bunnyStorageZone.Name}...");

    await Parallel.ForEachAsync(bunnyFiles, options, async (file, ct) =>
    {
        var attempt = 0;
        while (attempt < MaxAttempts)
        {
            try
            {
                await bunnyCDNStorage.DeleteObjectAsync(file.Path);
                deletedFiles.Add(file.Path);
                return;
            }
            catch
            {
                attempt++;

                if (attempt == 3)
                {
                    deletedFilesFail.Add(file.Path);
                    throw;
                }
                Thread.Sleep(50);
            }
        }
    });

    Console.WriteLine($"Deleted {deletedFiles.Count} files from Bunny {bunnyStorageZone.Name}");
    if (deletedFilesFail.Count > 0)
    {
        Console.WriteLine($"Failed to delete {deletedFilesFail.Count} files from Bunny {bunnyStorageZone.Name}:");
        foreach (var file in deletedFilesFail)
        {
            Console.Error.WriteLine($"- {file}");
        }
        return 1;
    }
}

var files = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories).ToList();
Console.WriteLine($"Found {files.Count} files to upload to Bunny {bunnyStorageZone.Name}");

ConcurrentBag<string> uploadedFiles = [];
ConcurrentBag<string> uploadedFilesFail = [];

await Parallel.ForEachAsync(files, options, async (file, ct) =>
{
    var relativePath = Path.GetRelativePath(localPath, file);

    var attempt = 0;
    while (attempt < MaxAttempts)
    {
        try
        {
            await bunnyCDNStorage.UploadAsync(file, $@"{bunnyStorageZone.Name}/{relativePath}");
            uploadedFiles.Add(file);
            return;
        }
        catch
        {
            attempt++;

            if (attempt == 3)
            {
                uploadedFilesFail.Add(file);
                throw;
            }
            Thread.Sleep(50);
        }
    }
});

Console.WriteLine($"Uploaded {uploadedFiles.Count} files to Bunny {bunnyStorageZone.Name}");
if (uploadedFilesFail.Count > 0)
{
    Console.WriteLine($"Failed to upload {uploadedFilesFail.Count} files to Bunny {storageZone}:");
    foreach (var file in uploadedFilesFail)
    {
        Console.Error.WriteLine($"- {file}");
    }
    return 1;
}

if (purgeAfterUpload) await PurgePullZone(bunnyStorageZone.PullZones, apiKey);

var githubOutputFile = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(githubOutputFile))
{
    await File.AppendAllTextAsync(githubOutputFile, $"files_uploaded={uploadedFiles.Count}{Environment.NewLine}");
    await File.AppendAllTextAsync(githubOutputFile, $"files_deleted={deletedFiles.Count}{Environment.NewLine}");
    await File.AppendAllTextAsync(githubOutputFile, $"files_failed_upload={uploadedFilesFail.Count}{Environment.NewLine}");
    await File.AppendAllTextAsync(githubOutputFile, $"files_failed_delete={deletedFilesFail.Count}{Environment.NewLine}");
    await File.AppendAllTextAsync(githubOutputFile, $"uploaded_files_json={JsonSerializer.Serialize(uploadedFiles)}{Environment.NewLine}");
}

return (uploadedFilesFail.IsEmpty && deletedFilesFail.IsEmpty) ? 0 : 1;


async Task<BunnyStorageZone> GetBunnyPullZone(string storageZone, string apiKey)
{
    using (HttpClient client = new())
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.bunny.net/storagezone?page=0&perPage=1000&includeDeleted=false");
        request.Headers.Add("AccessKey", apiKey);
        request.Headers.Add("accept", "application/json");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();

        var storageZones = JsonSerializer.Deserialize<List<BunnyStorageZone>>(responseContent);

        if (storageZones == null || !storageZones.Any()) throw new Exception("Storage zone not found");

        var selectedZone = storageZones.FirstOrDefault(z => z.Name.ToLower().Equals(storageZone.ToLower()) || z.Id.ToString().Equals(storageZones));
        if (selectedZone == null) throw new Exception($"Storage zone '{storageZone}' not found.");

        return selectedZone;
    }
}

async Task PurgePullZone(List<BunnyPullZone> pullZones, string apiKey)
{
    using (HttpClient client = new())
    {
        foreach (var pullZone in pullZones)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.bunny.net/pullzone/{pullZone.Id}/purgeCache");
            request.Headers.Add("AccessKey", apiKey);
            request.Headers.Add("accept", "application/json");
            var content = new StringContent(string.Empty);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
            await client.SendAsync(request);
        }
    }
}