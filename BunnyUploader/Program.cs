using BunnyCDN.Net.Storage;
using System.Collections.Concurrent;
using System.Text.Json;

const int MaxAttempts = 3;

var mainZone = Environment.GetEnvironmentVariable("INPUT_MAIN_ZONE") ?? "de";
var storageZone = Environment.GetEnvironmentVariable("INPUT_STORAGE_ZONE");
var apiKey = Environment.GetEnvironmentVariable("INPUT_API_KEY");
var localPath = Environment.GetEnvironmentVariable("INPUT_LOCAL_PATH");
var remotePath = Environment.GetEnvironmentVariable("INPUT_REMOTE_PATH") ?? "";
bool removeOldFiles = Convert.ToBoolean(Environment.GetEnvironmentVariable("INPUT_REMOVE_OLD_FILES"));

Console.WriteLine($"Main Zone: {mainZone}");
Console.WriteLine($"Storage Zone: {storageZone}");
Console.WriteLine($"API Key: {apiKey}");
Console.WriteLine($"Local Path: {localPath}");
Console.WriteLine($"Remote Path: {remotePath}");
Console.WriteLine($"Remove Old Files: {removeOldFiles}");

if (string.IsNullOrEmpty(storageZone))
{
    Console.Error.WriteLine("Error: The 'storage-zone' entry is required.");
    return 1;
}

if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("Error: The 'api-key' entry is required.");
    return 1;
}

if (string.IsNullOrEmpty(localPath))
{
    Console.Error.WriteLine("Error: The 'local-path' entry is required.");
    return 1;
}

var fullPath = Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? "./", localPath));
if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
{
    Console.Error.WriteLine($"Error: O caminho local '{fullPath}' não foi encontrado.");
    return 1;
}

var bunnyCDNStorage = new BunnyCDNStorage(storageZone, apiKey, mainZone);

var options = new ParallelOptions()
{
    MaxDegreeOfParallelism = 50 //https://docs.bunny.net/reference/edge-storage-api-limits
};

ConcurrentBag<string> deletedFiles = [];
ConcurrentBag<string> deletedFilesFail = [];

if (removeOldFiles)
{
    var bunnyFiles = await bunnyCDNStorage.GetStorageObjectsAsync(storageZone + "/");

    Console.WriteLine($"{storageZone}: {bunnyFiles.Count} files");
    Console.WriteLine($"Removing old files from Bunny {storageZone}...");

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

    Console.WriteLine($"Deleted {deletedFiles.Count} files from Bunny {storageZone}");
    if (deletedFilesFail.Count > 0)
    {
        Console.WriteLine($"Failed to delete {deletedFilesFail.Count} files from Bunny {storageZone}:");
        foreach (var file in deletedFilesFail)
        {
            Console.Error.WriteLine($"- {file}");
        }
        return 1;
    }
}

var files = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories).ToList();
Console.WriteLine($"Found {files.Count} files to upload to Bunny {storageZone}");

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
            await bunnyCDNStorage.UploadAsync(file, $@"{storageZone}/{relativePath}");
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

Console.WriteLine($"Uploaded {uploadedFiles.Count} files to Bunny {storageZone}");
if (uploadedFilesFail.Count > 0)
{
    Console.WriteLine($"Failed to upload {uploadedFilesFail.Count} files to Bunny {storageZone}:");
    foreach (var file in uploadedFilesFail)
    {
        Console.Error.WriteLine($"- {file}");
    }
    return 1;
}

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