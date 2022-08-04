using System.Threading.Tasks;
using Juice.Storage.Abstractions;
using Xunit;

namespace Juice.Storage.Tests
{
    public class FtpStorageTest
    {
        private bool _test = false;
        public FtpStorageTest()
        {
            _test = true;
        }

        [Fact(DisplayName = "File not exists and should be create")]
        public async Task File_should_create_Async()
        {
            if (!_test)
            {
                return;
            }

            var storage = new Local.FTPStorage()
                .Configure(new StorageEndpoint(@"127.0.0.1/Working", default, "demo", "demo"));

            await SharedTests.File_should_create_Async(storage);
        }

        [Fact(DisplayName = "File exists and raise an error")]
        public async Task File_create_should_error_Async()
        {
            if (!_test)
            {
                return;
            }

            var storage = new Local.FTPStorage()
                .Configure(new StorageEndpoint(@"127.0.0.1", default, "demo", "demo"));

            await SharedTests.File_create_should_error_Async(storage);
        }

        [Fact(DisplayName = "File exists and add copy number")]
        public async Task File_create_should_add_copy_number_Async()
        {
            if (!_test)
            {
                return;
            }

            var storage = new Local.FTPStorage()
                .Configure(new StorageEndpoint(@"127.0.0.1", default, "demo", "demo"));

            await SharedTests.File_create_should_add_copy_number_Async(storage);
        }
    }
}
