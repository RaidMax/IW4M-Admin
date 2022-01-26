namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class UpdatedAliasResponse : BaseMetaResponse
    {
        public string Name { get; set; }
        public string IPAddress { get; set; } = "--";

        public override bool Equals(object obj)
        {
            if (obj is UpdatedAliasResponse resp)
            {
                return resp.Name.StripColors() == Name.StripColors() && resp.IPAddress == IPAddress;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{Name.StripColors()}{IPAddress}".GetStableHashCode();
        }
    }
}