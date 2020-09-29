namespace ApplicationTests.Mocks
{
    public interface IScriptResolverMock
    {
        string Value { get; set; }
    }

    public class ScriptResolverMock : IScriptResolverMock
    {
        public string Value { get; set; }
    }
    public interface IScriptResolverGenericMock<T, V>
    {
        T Value { get; set; }
        V Value2 { get; set; }
    }

    public class ScriptResolverGenericMock<T, V> : IScriptResolverGenericMock<T, V>
    {
        public T Value { get; set; }
        public V Value2 { get; set; }
    }
}
