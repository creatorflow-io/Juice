using System.IO;
using System.Text;
using System.Threading.Tasks;
using Juice.Storage.Abstractions;
using Xunit;

namespace Juice.Storage.Tests
{
    public static class SharedTests
    {
        public static async Task File_should_create_Async(IStorage storage)
        {
            var generator = new Services.DefaultStringIdGenerator();
            var file = @"Test\" + generator.GenerateRandomId(26) + ".mxf";

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

        public static async Task File_create_should_error_Async(IStorage storage)
        {
            var generator = new Services.DefaultStringIdGenerator();
            var file = generator.GenerateRandomId(26) + ".mxf";
            var createdFile = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);

            Assert.True(storage.ExistsAsync(createdFile, default).GetAwaiter().GetResult());
            Assert.Equal(file, createdFile);

            await Assert.ThrowsAsync<IOException>(async () =>
            {
                await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);
            });

            await storage.DeleteAsync(createdFile, default);
        }

        public static async Task File_create_should_add_copy_number_Async(IStorage storage)
        {
            var generator = new Services.DefaultStringIdGenerator();
            var name = generator.GenerateRandomId(26);
            var file = name + ".mxf";
            var file1 = name + "(1).mxf";
            var file2 = name + "(2).mxf";
            var file3 = name + "(3).mxf";

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
    }
}
