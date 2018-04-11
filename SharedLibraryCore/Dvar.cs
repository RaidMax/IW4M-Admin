namespace SharedLibraryCore
{
    public class Dvar<T>
    {
        public string Name { get; private set; }
        public T Value;

        public Dvar(string name)
        {
            Name = name;
        }
    }
}
