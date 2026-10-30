namespace Quorum.App.Features.Shared;

public enum CommandStatus
{
    Ok,
    Refused,
    NotFound,
}

public sealed record CommandOutcome(CommandStatus Status, Guid? Id = null)
{
    public static CommandOutcome Ok(Guid? id = null) => new(CommandStatus.Ok, id);

    public static CommandOutcome Refused() => new(CommandStatus.Refused);

    public static CommandOutcome NotFound() => new(CommandStatus.NotFound);

    public bool Succeeded => Status == CommandStatus.Ok;
}
