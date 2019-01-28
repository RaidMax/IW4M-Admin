namespace SharedLibraryCore.Interfaces
{
    public interface IEventParserConfiguration
    {
        string GameDirectory { get; set; }
        ParserRegex Say { get; set; }
        ParserRegex Join { get; set; }
        ParserRegex Quit { get; set; }
        ParserRegex Kill { get; set; }
        ParserRegex Damage { get; set; }
        ParserRegex Action { get; set; }
    }
}
