using System.Collections.Generic;

namespace SharedLibraryCore.Helpers;

public class ParsedInputResult<TResult>
{
    public TResult? Result { get; set; }
    public string? RawInput { get; set; }
    public List<string> ErrorMessages { get; set; } = [];

    public ParsedInputResult<TResult> WithError(string errorMessage)
    {
        ErrorMessages.Add(errorMessage);
        return this;
    }
}
