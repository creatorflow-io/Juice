using System.Security.Cryptography;
using System.Text;
using Juice.Workflows.Builder;
using Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate;
using Juice.Workflows.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Yaml.Builder
{
    internal class YamlFileWorkflowContextBuilder : IWorkflowContextBuilder
    {
        public int Priority => 1;
        private string _directory = "workflows";

        private static Dictionary<string, WorkflowContextBuilder> _builders = new Dictionary<string, WorkflowContextBuilder>();

        private static Dictionary<string, string> _fileHash = new Dictionary<string, string>();

        private readonly IServiceProvider _serviceProvider;

        public YamlFileWorkflowContextBuilder(
            IServiceProvider serviceProvider
        )
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<WorkflowContext> BuildAsync(string workflowId,
            string instanceId,
            CancellationToken token)
        {
            var file = Path.Combine(_directory, workflowId + ".yaml");

            var hash = await GetMD5Async(file, token);
            var build = !_fileHash.ContainsKey(file)
                || _fileHash[file] != hash
                || !_builders.ContainsKey(file)
                ;

            if (build)
            {
                _fileHash[file] = hash;

                if (!_builders.ContainsKey(file))
                {
                    var builder = _serviceProvider.GetRequiredService<WorkflowContextBuilder>();
                    _builders.Add(file, builder);
                }
                var yml = await File.ReadAllTextAsync(file);
                return _builders[file].Build(yml, new WorkflowRecord(instanceId, workflowId, default, default), true);

            }
            var nullYml = default(string?);
            return _builders[file].Build(nullYml, new WorkflowRecord(instanceId, workflowId, default, default), false);
        }

        public Task<bool> ExistsAsync(string workflowId, CancellationToken token)
            => Task.FromResult(File.Exists(Path.Combine(_directory, workflowId + ".yaml")));

        public void SetWorkflowsDirectory(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _directory = directory;
            }
        }

        private static async Task<string> GetMD5Async(string filePath, CancellationToken token)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            return ToHex(await md5.ComputeHashAsync(stream));
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

    }
}
