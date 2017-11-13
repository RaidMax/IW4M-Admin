using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Sockets;

namespace SharedLibrary.Network
{
    public static class RCON
    {
        enum QueryType
        {
            GET_STATUS,
            GET_INFO,
            DVAR,
            COMMAND,
        }

        static string[] SendQuery(QueryType Type, Server QueryServer,  string Parameters = "")
        {
            var ServerOOBConnection = new UdpClient();
            ServerOOBConnection.Client.SendTimeout = 5000;
            ServerOOBConnection.Client.ReceiveTimeout = 5000;
            var Endpoint = new IPEndPoint(IPAddress.Parse(QueryServer.GetIP()), QueryServer.GetPort());

            string QueryString = String.Empty;

            switch (Type)
            {
                case QueryType.DVAR:
                case QueryType.COMMAND:
                    QueryString = $"ÿÿÿÿrcon {QueryServer.Password} {Parameters}";
                    break;
                case QueryType.GET_STATUS:
                    QueryString = "ÿÿÿÿ getstatus";
                    break;
            }

            byte[] Payload = GetRequestBytes(QueryString);

            int attempts = 0;
            retry:

            try
            {
                ServerOOBConnection.Connect(Endpoint);
                ServerOOBConnection.Send(Payload, Payload.Length);

                byte[] ReceiveBuffer = new byte[8192];
                StringBuilder QueryResponseString = new StringBuilder();

                do
                {
                    ReceiveBuffer = ServerOOBConnection.Receive(ref Endpoint);
                    QueryResponseString.Append(Encoding.ASCII.GetString(ReceiveBuffer).TrimEnd('\0'));
                } while (ServerOOBConnection.Available > 0 && ServerOOBConnection.Client.Connected);

                ServerOOBConnection.Close();

                if (QueryResponseString.ToString().Contains("Invalid password"))
                    throw new Exceptions.NetworkException("RCON password is invalid");
                if (QueryResponseString.ToString().Contains("rcon_password"))
                    throw new Exceptions.NetworkException("RCON password has not been set");

                int num = int.Parse("0a", System.Globalization.NumberStyles.AllowHexSpecifier);
                string[] SplitResponse = QueryResponseString.ToString().Split(new char[] { (char)num }, StringSplitOptions.RemoveEmptyEntries);
                return SplitResponse;
            }

            catch (Exceptions.NetworkException e)
            {
                throw e;
            }

            catch (Exception e)
            {
                attempts++;
                if (attempts > 2)
                {
                    var ne = new Exceptions.NetworkException("Could not communicate with the server");
                    ne.Data["internal_exception"] = e.Message;
                    ne.Data["server_address"] = ServerOOBConnection.Client.RemoteEndPoint.ToString();
                    ServerOOBConnection.Close();
                    throw ne;
                }

                Thread.Sleep(1000);
                    goto retry;
            }
        }

        public static async Task<DVAR<T>> GetDvarAsync<T>(this Server server, string dvarName)
        {
            string[] LineSplit = await Task.FromResult(SendQuery(QueryType.DVAR, server, dvarName));

            if (LineSplit.Length != 3)
            {
                var e = new Exceptions.DvarException($"DVAR \"{dvarName}\" does not exist");
                e.Data["dvar_name"] = dvarName;
                throw e;
            }

            string[] ValueSplit = LineSplit[1].Split(new char[] { '"' }, StringSplitOptions.RemoveEmptyEntries);

            if (ValueSplit.Length != 5)
            {
                var e = new Exceptions.DvarException($"DVAR \"{dvarName}\" does not exist");
                e.Data["dvar_name"] = dvarName;
                throw e;
            }

            string DvarName = Regex.Replace(ValueSplit[0], @"\^[0-9]", "");
            string DvarCurrentValue = Regex.Replace(ValueSplit[2], @"\^[0-9]", "");
            string DvarDefaultValue = Regex.Replace(ValueSplit[4], @"\^[0-9]", "");

            return new DVAR<T>(DvarName) { Value = (T)Convert.ChangeType(DvarCurrentValue, typeof(T)) };
        }

        public static async Task SetDvarAsync(this Server server, string dvarName, object dvarValue)
        {
            await Task.FromResult(SendQuery(QueryType.DVAR, server, $"set {dvarName} {dvarValue}"));
        }

        public static async Task<string[]> ExecuteCommandAsync(this Server server, string commandName)
        {
            return await Task.FromResult(SendQuery(QueryType.COMMAND, server, commandName).Skip(1).ToArray());
        }

        public static async Task<List<Player>> GetStatusAsync(this Server server)
        {
            string[] response = await Task.FromResult(SendQuery(QueryType.DVAR, server, "status"));
            return Utilities.PlayersFromStatus(response);
        }

        static byte[] GetRequestBytes(string Request)
        {
            Byte[] initialRequestBytes = Encoding.Unicode.GetBytes(Request);
            Byte[] fixedRequest = new Byte[initialRequestBytes.Length / 2];

            for (int i = 0; i < initialRequestBytes.Length; i++)
                if (initialRequestBytes[i] != 0)
                    fixedRequest[i / 2] = initialRequestBytes[i];

            return fixedRequest;
        }
    }
}
