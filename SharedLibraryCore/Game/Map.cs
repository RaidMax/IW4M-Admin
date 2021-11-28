namespace SharedLibraryCore
{
    public class Map
    {
        public string Name { get; set; }
        public string Alias { get; set; }

        public override string ToString() => Alias;
    }
}
