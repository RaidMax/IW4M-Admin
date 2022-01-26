namespace SharedLibraryCore.Dtos
{
    public class SharedInfo
    {
        public virtual bool Sensitive { get; set; }
        public bool Show { get; set; } = true;
        public int Id { get; set; }
    }
}