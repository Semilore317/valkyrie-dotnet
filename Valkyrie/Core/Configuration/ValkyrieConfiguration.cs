namespace Valkyrie.Core.Configuration
{
    class ValkyrieConfiguration
    {
        public ValkyrieSettings? ValkyrieSettings { get; set; }
    }

    class TradingServerSettings
    {
        public required string ServerName { get; set; }
        public int Port { get; set; }
        public required string Host { get; set; }
    }
}
