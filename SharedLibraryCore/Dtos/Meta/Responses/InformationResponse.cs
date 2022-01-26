namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class InformationResponse : BaseMetaResponse
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string ToolTipText { get; set; }
    }
}