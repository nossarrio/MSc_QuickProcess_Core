using Azure.Storage.Blobs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickProcess
{
    public class Utility
    {
        public static class Blob
        {
            public static async Task<List<string>> getBlobs(string storage_key, string containerName)
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(storage_key);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobs = containerClient.GetBlobs();
                List<string> blonNames = new List<string>();


                foreach (var item in blobs)
                {
                    blonNames.Add(item.Name);
                }

                return blonNames;
            }

            public static async Task<bool> blobExist(string storage_key, string containerName, string fileName)
            {
                var service = new BlobServiceClient(storage_key);
                var client = service.GetBlobContainerClient(containerName.ToLower());
                var blolClient = client.GetBlobClient(fileName);
                return await blolClient.ExistsAsync();
            }

            public static async void renameBlob(string storage_key, string containerName, string oldFileName, string newFileName)
            {
                var fileContent = await readBlob(storage_key, containerName, oldFileName);

                var storageAccount = CloudStorageAccount.Parse(storage_key);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference(containerName);

                //create new blob and update with old blob file content
                CloudBlockBlob blob = container.GetBlockBlobReference(newFileName);
                var options = new BlobRequestOptions()
                {
                    ServerTimeout = TimeSpan.FromMinutes(10)
                };
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(fileContent), false))
                {
                    await blob.UploadFromStreamAsync(stream);
                }

                //delete old blob file
                blob = container.GetBlockBlobReference(oldFileName);
                await blob.DeleteAsync();
            }

            public static async Task<string> readBlob(string storage_key, string containerName, string fileName)
            {
                var service = new BlobServiceClient(storage_key);
                var client = service.GetBlobContainerClient(containerName.ToLower());
                var blolClient = client.GetBlobClient(fileName);
                var blobDownloadInfo = await blolClient.DownloadAsync();
                StreamReader sr = new StreamReader(blobDownloadInfo.Value.Content);
                return sr.ReadToEnd().ToString();
            }

            public static async Task<bool> writeBlob(string storage_key, string containerName, string fileName, string content)
            {
                var storageAccount = CloudStorageAccount.Parse(storage_key);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference(containerName.ToLower());

                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
                await blob.DeleteIfExistsAsync();
                var options = new BlobRequestOptions()
                {
                    ServerTimeout = TimeSpan.FromMinutes(10)
                };
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(content), false))
                {
                    await blob.UploadFromStreamAsync(stream);
                }

                return true;
            }

            public static async Task<bool> creatBlobContainer(string storage_key, string containerName)
            {
                //create container
                var storageAccount = CloudStorageAccount.Parse(storage_key);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference(containerName);
                await container.CreateIfNotExistsAsync();
                return true;
            }

        }

        public static class Security
        {
            public static string HASH256(string text)
            {
                return QuickProcess.Service.HASH256(text);
            }

            public static string EncryptText(string clearText)
            {
                return QuickProcess.Service.EncryptText(clearText);
            }

            public static string DecryptText(string clearText)
            {
                return QuickProcess.Service.DecryptText(clearText);
            }
        }
    }
}
