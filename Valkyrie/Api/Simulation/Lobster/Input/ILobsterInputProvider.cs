using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Core.Configuration;

namespace Valkyrie.Api.Simulation.Lobster.Input;

public interface ILobsterInputProvider
{
    LobsterDataFormat Format { get; }
    LobsterInput Open(HistoricalReplayInstrument instrument);
}