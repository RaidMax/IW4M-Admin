namespace SharedLibraryCore
{
    public class DVAR<T>
    {
        public string Name { get; private set; }
        public T Value;

        public DVAR(string name)
        {
            Name = name;
        }
    }
}
