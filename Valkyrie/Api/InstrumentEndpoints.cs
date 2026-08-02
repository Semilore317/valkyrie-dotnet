using Valkyrie.Instruments;

namespace Valkyrie.Api;

public static class InstrumentEndpoints
{
    public static void MapInstrumentEndpoints(
        this WebApplication app)
    {
        app.MapGet(
            "/instruments",
            (InstrumentCatalogue catalogue) =>
                    Results.Ok(catalogue.Instruments)
            );
    }
}