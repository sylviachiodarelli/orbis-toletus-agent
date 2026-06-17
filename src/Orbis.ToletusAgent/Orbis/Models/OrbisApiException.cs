namespace Orbis.ToletusAgent.Orbis;

public sealed class OrbisApiException : Exception
{
    public OrbisApiException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
