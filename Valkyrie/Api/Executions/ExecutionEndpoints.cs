namespace Valkyrie.Api.Executions;

public static class ExecutionEndpoints
{
    public static void MapExecutionEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions", () =>
        {
            var session = new TradingSessionResponse(Guid.NewGuid(), DateTime.UtcNow);

            return Results.Created(
                $"sessions/{session.SessionId}",
                session
            );
        });

        app.MapGet("/sessions/{sessionId:guid}/executions", (
            Guid sessionId,
            long? securityId,
            IExecutionJournal journal) =>
        {
            var executions = journal.GetExecutions(sessionId, securityId);
            return Results.Ok(executions);
        });
    }


    private record TradingSessionResponse(
        Guid SessionId,
        DateTime CreatedAt
    );
}