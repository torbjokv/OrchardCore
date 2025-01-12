using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OrchardCore.Environment.Shell.Configuration
{
    public class ShellConfigurationSources : IShellConfigurationSources
    {
        private readonly string _container;

        // This could be optimized by locking per tenant, but worst case is that 
        // two tenants are blocking each other when the configs are updated. Theorically,
        // as this storing configs in files, this is supposed to be used with few tenants.
        // So we are not optimizing with a distinct semarphore per tenant. 

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public ShellConfigurationSources(IOptions<ShellOptions> shellOptions)
        {
            // e.g., App_Data/Sites
            _container = Path.Combine(shellOptions.Value.ShellsApplicationDataPath, shellOptions.Value.ShellsContainerName);
            Directory.CreateDirectory(_container);
        }

        public void AddSources(string tenant, IConfigurationBuilder builder)
        {
            builder
                .AddJsonFile(Path.Combine(_container, tenant, "appsettings.json"), optional: true);
        }

        public async Task SaveAsync(string tenant, IDictionary<string, string> data)
        {
            await _semaphore.WaitAsync();

            try
            {
                var tenantFolder = Path.Combine(_container, tenant);
                var appsettings = Path.Combine(tenantFolder, "appsettings.json");

                JObject config;
                if (File.Exists(appsettings))
                {
                    using (var file = File.OpenText(appsettings))
                    {
                        using (var reader = new JsonTextReader(file))
                        {
                            config = await JObject.LoadAsync(reader);
                        }
                    }
                }
                else
                {
                    config = new JObject();
                }

                foreach (var key in data.Keys)
                {
                    if (data[key] != null)
                    {
                        config[key] = data[key];
                    }
                    else
                    {
                        config.Remove(key);
                    }
                }

                Directory.CreateDirectory(tenantFolder);

                using (var file = File.CreateText(appsettings))
                {
                    using (var writer = new JsonTextWriter(file) { Formatting = Formatting.Indented })
                    {
                        await config.WriteToAsync(writer);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
