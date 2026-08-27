namespace CRT.Core.Models;

/// <summary>
/// A user-facing validation failure (bad load bounds, corrupt files, …).
/// The message is shown verbatim in an error dialog — mirrors the Python app,
/// which raises <c>ValueError</c> and surfaces <c>str(e)</c>.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
