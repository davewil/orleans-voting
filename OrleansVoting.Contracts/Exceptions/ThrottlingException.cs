using System.Runtime.Serialization;

namespace OrleansVoting.Contracts.Exceptions;

[GenerateSerializer]
[Alias("OrleansVoting.ThrottlingException")]
public class ThrottlingException : Exception
{
    public ThrottlingException(string message) : base(message) { }
    public ThrottlingException(string message, Exception innerException) : base(message, innerException) { }
}
