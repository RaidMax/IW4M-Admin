using System;

namespace SharedLibraryCore.Helpers
{
    public class ParseEnum<T>
    {
        public static T Get(string e, Type type)
        {
            try
            {
                return (T)Enum.Parse(type, e);
            }

            catch (Exception)
            {
                return (T)(Enum.GetValues(type).GetValue(0));
            }
        }
    }
}
