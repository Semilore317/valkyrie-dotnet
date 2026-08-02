using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Valkyrie.Core.Configuration;
using Valkyrie.Logging;

namespace Valkyrie.Core
{
    sealed class Valkyrie : BackgroundService, IValkyrie
    {
        private readonly ITextLogger _logger;
        private readonly ValkyrieConfiguration _config;

        public Valkyrie(
            ITextLogger textLogger,
            IOptions<ValkyrieConfiguration> config
        )
        {
            _logger = textLogger ?? throw new ArgumentNullException(nameof(textLogger));
            _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        }

        public Task Run(CancellationToken token)
        {
            return ExecuteAsync(token);
            // the cancellation token makes it so that we can cancel the "run loop"
            // kinda similar to the way games run on a loop or tk runs on a mainLoop
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("Valkyrie.Core", "Trading Engine Server Started");
            // the server technically doesn't need a loop, but it is here to keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // should catch during engine shutdown
                }
            }

            _logger.Info("Valkyrie.Core", "Trading Engine Server Stopped");
        }
    }
}