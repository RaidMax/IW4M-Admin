using System;

namespace SharedLibraryCore.Interfaces;

public interface IScriptPluginFactory
{
    object CreateScriptPlugin(Type type, string fileName);
}
