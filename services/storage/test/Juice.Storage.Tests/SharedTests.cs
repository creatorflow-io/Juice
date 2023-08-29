using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Juice.Storage.Abstractions;
using Juice.Storage.Dto;
using Juice.Storage.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Storage.Tests
{
    public static class SharedTests
    {
        #region StorageProvider
        public static async Task File_should_create_Async(IStorageProvider storage)
        {
            var generator = new Services.DefaultStringIdGenerator();
            var file = @"Test\" + generator.GenerateRandomId(26) + ".txt";

            var createdFile = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);

            Assert.True(storage.ExistsAsync(createdFile, default).GetAwaiter().GetResult());
            Assert.Equal(file, createdFile);

            byte[] byteArray = Encoding.UTF8.GetBytes("abcxyz123");

            using (var stream = new MemoryStream(byteArray))
            {
                await storage.WriteAsync(createdFile, stream, 0, new TransferOptions(), default);
            }

            using (var stream = new MemoryStream(byteArray))
            {
                await storage.WriteAsync(createdFile, stream, stream.Length, new TransferOptions(), default);
            }

            using (var stream = await storage.ReadAsync(createdFile, default))
            using (var reader = new StreamReader(stream))
            {
                Assert.Equal("abcxyz123abcxyz123", reader.ReadToEnd());
            }

            await storage.DeleteAsync(createdFile, default);
        }

        public static async Task File_create_should_error_Async(IStorageProvider storage)
        {
            var generator = new Services.DefaultStringIdGenerator();
            var file = generator.GenerateRandomId(26) + ".txt";
            var createdFile = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);

            Assert.True(storage.ExistsAsync(createdFile, default).GetAwaiter().GetResult());
            Assert.Equal(file, createdFile);

            await Assert.ThrowsAsync<IOException>(async () =>
            {
                await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);
            });

            await storage.DeleteAsync(createdFile, default);
        }

        public static async Task File_create_should_add_copy_number_Async(IStorageProvider storage)
        {
            var generator = new Services.DefaultStringIdGenerator();
            var name = generator.GenerateRandomId(26);
            var file = name + ".txt";
            var file1 = name + "(1).txt";
            var file2 = name + "(2).txt";
            var file3 = name + "(3).txt";

            var createdFile = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);

            Assert.True(storage.ExistsAsync(createdFile, default).GetAwaiter().GetResult());
            Assert.Equal(file, createdFile);

            var createdFile1 = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.AscendedCopyNumber }, default);

            Assert.True(storage.ExistsAsync(createdFile1, default).GetAwaiter().GetResult());
            Assert.Equal(file1, createdFile1);

            var createdFile2 = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.AscendedCopyNumber }, default);

            Assert.True(storage.ExistsAsync(createdFile2, default).GetAwaiter().GetResult());
            Assert.Equal(file2, createdFile2);

            var createdFile3 = await storage.CreateAsync(file2, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.AscendedCopyNumber }, default);

            Assert.True(storage.ExistsAsync(createdFile3, default).GetAwaiter().GetResult());
            Assert.Equal(file3, createdFile3);

            await Task.Delay(1000);
            await storage.DeleteAsync(createdFile, default);
            await storage.DeleteAsync(createdFile1, default);
            await storage.DeleteAsync(createdFile2, default);
            await storage.DeleteAsync(createdFile3, default);

        }

        #endregion

        #region UploadManager

        public static async Task File_upload_Async(IUploadManager uploadManager, ITestOutputHelper testOutput)
        {

            var file = new FileInfo(@"C:\Workspace\dotnet-sdk.exe");
            if (file.Exists)
            {

                var generator = new Services.DefaultStringIdGenerator();
                var fileName = @"Test\" + generator.GenerateRandomId(26) + ".zzz";
                string? contentType = default;
                string? correlationId = default;

                var fileInfo = new InitialFileInfo(fileName, file.Length, contentType, fileName, DateTimeOffset.Now, correlationId, default, FileExistsBehavior.AscendedCopyNumber);

                var operationResult = await uploadManager.InitAsync(fileInfo, default);

                var uploadId = operationResult.UploadId;
                var sectionSize = operationResult.SectionSize;
                var createdFileName = operationResult.Name;

                testOutput.WriteLine("Section size {0}", sectionSize);
                long offset = 0;

                while (offset < file.Length)
                {
                    using var istream = File.OpenRead(file.FullName);
                    istream.Seek(offset, SeekOrigin.Begin);

                    var bufferSize = (int)Math.Min(sectionSize, (file.Length - offset));
                    testOutput.WriteLine("Buffer size {0}", bufferSize);

                    var buffer = new byte[bufferSize];
                    await istream.ReadAsync(buffer, 0, bufferSize);

                    using var memStream = new MemoryStream(buffer);

                    await uploadManager.UploadAsync(uploadId, memStream, offset, default);

                    testOutput.WriteLine("Uploaded from {0} to {1}", offset, offset + memStream.Length);
                    offset += memStream.Length;

                }

                var md5 = MD5.Create();
                {
                    using var src = File.OpenRead(file.FullName);
                    var srcHash = ToHex(await md5.ComputeHashAsync(src));
                    var destHash = await uploadManager.Storage.GetMD5Async(createdFileName, default);

                    testOutput.WriteLine("Original file hash {0}", srcHash);
                    testOutput.WriteLine("Uploaded file hash {0}", destHash);
                    Assert.Equal(srcHash, destHash);
                }

                await uploadManager.Storage.DeleteAsync(createdFileName, default);
            }
        }

        private static string ToHex(byte[] bytes, bool upperCase = false)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
            {
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            }

            return result.ToString();
        }

        #endregion

    }
}
