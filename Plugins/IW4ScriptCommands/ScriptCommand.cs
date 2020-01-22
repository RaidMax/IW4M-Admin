using SharedLibraryCore;
using System.Linq;
using System.Threading.Tasks;

namespace IW4ScriptCommands
{
    /// <summary>
    /// Contains basic properties for command information read by gsc
    /// </summary>
    class ScriptCommand
    {
        /// <summary>
        /// Name of the command to execute
        /// </summary>
        public string CommandName { get; set; }

        /// <summary>
        /// Target client number
        /// </summary>
        public int ClientNumber { get; set; }

        /// <summary>
        /// Arguments for the script function itself
        /// </summary>
        public string[] CommandArguments { get; set; } = new string[0];

        public override string ToString() => string.Join(";", new[] { CommandName, ClientNumber.ToString() }.Concat(CommandArguments).Select(_arg => _arg.Replace(";", "")));

        /// <summary>
        /// Executes the command 
        /// </summary>
        /// <param name="server">server to execute the command on</param>
        /// <returns></returns>
        public async Task Execute(Server server) => await server.SetDvarAsync("sv_iw4madmin_command", ToString());
    }
}
