namespace SharedLibraryCore
{
    public class Dvar<T>
    {
        public string Name { get; set; }
        public T Value { get; set; }
        public T DefaultValue { get; set; }
        public T LatchedValue { get; set; }
        public string Domain { get; set; }
    }
}