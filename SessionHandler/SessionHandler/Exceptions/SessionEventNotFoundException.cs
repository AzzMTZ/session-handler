namespace SessionHandler.Exceptions;

/// <summary>Thrown when a lookup targets a session event id that does not exist.</summary>
public class SessionEventNotFoundException : Exception
{
    public SessionEventNotFoundException(int id)
        : base($"No session event found with id '{id}'.")
    {
        Id = id;
    }

    public int Id { get; }
}
